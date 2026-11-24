using LiteExplorer.MVVM.Models;
using LiteExplorer.Services;
using System.IO;
using Xunit;

namespace LiteExplorer.UnitTests;

public sealed class LoggingTests : IDisposable
{
    private readonly TempFolder temp = new();
    private DateTime now = new(2026, 1, 1, 12, 0, 0);

    public void Dispose() => temp.Dispose();

    private AuditService CreateAudit() => new(temp.Root, () => now);

    private long TotalBytes() => Directory.GetFiles(temp.Root).Sum(f => new FileInfo(f).Length);

    [Fact]
    public void EntriesSurviveRestartNewestFirst()
    {
        var writer = CreateAudit();
        writer.Log(AuditEventType.UsbConnected, @"E:\", "inserted");
        writer.Log(AuditEventType.FileCopied, @"E:\a.pdf", "copied");
        writer.Log(AuditEventType.SecurityBlocked, @"C:\Windows", "blocked", false);

        var entries = CreateAudit().GetRecentEntries(10);

        Assert.Equal(new[] { AuditEventType.SecurityBlocked, AuditEventType.FileCopied, AuditEventType.UsbConnected }, entries.Select(e => e.EventType));
        Assert.Equal(new[] { false, true, true }, entries.Select(e => e.Success));
        Assert.Equal(@"C:\Windows", entries[0].Target);
        Assert.Equal(2, writer.GetRecentEntries(2).Count);
    }

    [Fact]
    public void ClearRemovesEverything()
    {
        var audit = CreateAudit();
        audit.Log(AuditEventType.FileOpened, "x");
        audit.ClearLogs();

        Assert.Empty(audit.GetRecentEntries());
    }

    [Fact]
    public void LineBreaksCannotForgeEntries()
    {
        var audit = CreateAudit();
        audit.Log(AuditEventType.FileOpened, "x", "first\r\n[2026-01-01 00:00:00] [AdminAccess] [SUCCESS] Target: fake | Details: ");

        Assert.Single(audit.GetRecentEntries());
    }

    [Fact]
    public void ParsingToleratesDamagedLines()
    {
        Assert.Null(AuditEntry.Parse(null));
        Assert.Null(AuditEntry.Parse("   "));
        Assert.Equal(string.Empty, AuditEntry.Parse("corrupt-line")?.Target);
    }

    [Fact]
    public void OldDaysAreDeleted()
    {
        var log = new RollingLog(temp.Root, "audit", 3, 1_000_000, () => now);
        for (int day = 0; day < 10; day++)
        {
            log.Append($"day {day}\n");
            now = now.AddDays(1);
        }

        Assert.Equal(new[] { "day 7", "day 8", "day 9" }, log.ReadLines());
    }

    [Fact]
    public void TotalSizeStaysWithinLimitAndKeepsTheNewest()
    {
        const long limit = 500_000;
        var log = new RollingLog(temp.Root, "audit", 30, limit, () => now);
        string line = new('x', 990);
        for (int i = 0; i < 3000; i++)
        {
            log.Append($"{i:D8} {line}\n");
            // One line may cross the part boundary.
            Assert.True(TotalBytes() <= limit + 1000);
        }

        var lines = log.ReadLines();
        Assert.StartsWith("00002999", lines[^1]);
        Assert.True(lines.Count > 300);
    }

    [Fact]
    public void LoweringLimitsDeletesImmediatelyAndOtherFilesAreKept()
    {
        temp.Write("audit-20250101-001.log", "old");
        temp.Write("audit-notes.log", "unrelated");
        temp.Write("crash-20250101-001.log", "other log");
        var log = new RollingLog(temp.Root, "audit", 365 * 3, 1_000_000, () => now);

        log.SetLimits(7, 1_000_000);

        Assert.False(temp.Exists("audit-20250101-001.log"));
        Assert.True(temp.Exists("audit-notes.log"));
        Assert.True(temp.Exists("crash-20250101-001.log"));
    }

    [Fact]
    public void CrashReportsAreWritten()
    {
        CrashLogger.LogException(new InvalidOperationException("outer problem", new IOException("inner problem")), "Unit tests");
        CrashLogger.LogMessage("plain message", "TEST");

        string content = File.ReadAllText(CrashLogger.LogFilePath);
        Assert.Contains("[CRASH REPORT]", content);
        Assert.Contains("outer problem", content);
        Assert.Contains("inner problem", content);
        Assert.Contains("plain message", content);
    }

    [Fact]
    public void LogLimitsInConfigurationAreBounded()
    {
        string path = temp.Write("config.json", "{\"LogRetentionDays\":0,\"LogMaxSizeMegabytes\":99999}");

        var config = new ConfigurationService(path).Load();

        Assert.Equal(1, config.LogRetentionDays);
        Assert.Equal(AppConfiguration.MaximumLogMaxSizeMegabytes, config.LogMaxSizeMegabytes);
    }
}
