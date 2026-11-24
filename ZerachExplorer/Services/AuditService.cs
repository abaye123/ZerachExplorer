using ZerachExplorer.MVVM.Models;
using System.IO;

namespace ZerachExplorer.Services;

public class AuditService : IAuditService
{
    private readonly RollingLog log;

    // The shared configuration folder is read-only for regular users, so the log is per user.
    public static AuditService Instance { get; } = new(CrashLogger.LogDirectory);

    public AuditService(string directory, Func<DateTime>? clock = null)
    {
        log = new RollingLog(directory, "audit", AppConfiguration.DefaultLogRetentionDays,
            AppConfiguration.DefaultLogMaxSizeMegabytes * 1024L * 1024L, clock);
    }

    public string LogFilePath => log.CurrentPath;

    public void SetLimits(int retentionDays, int maxSizeMegabytes) =>
        Guard("applying log limits", () => log.SetLimits(retentionDays, maxSizeMegabytes * 1024L * 1024L));

    public void Log(AuditEventType eventType, string target, string? details = null, bool success = true)
    {
        var entry = new AuditEntry
        {
            Timestamp = DateTime.UtcNow,
            EventType = eventType,
            Target = SingleLine(target),
            Details = SingleLine(details),
            Success = success
        };

        Guard("writing", () => log.Append(entry + Environment.NewLine));
    }

    public IReadOnlyList<AuditEntry> GetRecentEntries(int count = 100)
    {
        IReadOnlyList<AuditEntry> entries = Array.Empty<AuditEntry>();
        Guard("reading", () => entries = log.ReadLines().TakeLast(count).Reverse()
            .Select(AuditEntry.Parse).OfType<AuditEntry>().ToList());
        return entries;
    }

    public void ClearLogs() => Guard("clearing", log.Clear);

    private static string SingleLine(string? text) => (text ?? string.Empty).ReplaceLineEndings(" ");

    /// <summary>A failing audit log must not stop the recorded operation, but it must not be silent either.</summary>
    private static void Guard(string action, Action body)
    {
        try
        {
            body();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CrashLogger.LogException(ex, $"Audit log: {action}");
        }
    }
}
