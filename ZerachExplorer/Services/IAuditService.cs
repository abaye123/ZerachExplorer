using ZerachExplorer.MVVM.Models;

namespace ZerachExplorer.Services;

public interface IAuditService
{
    string LogFilePath { get; }
    void SetLimits(int retentionDays, int maxSizeMegabytes);
    void Log(AuditEventType eventType, string target, string? details = null, bool success = true);
    IReadOnlyList<AuditEntry> GetRecentEntries(int count = 100);
    void ClearLogs();
}
