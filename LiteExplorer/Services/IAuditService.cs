using LiteExplorer.MVVM.Models;
using System.Collections.Generic;

namespace LiteExplorer.Services;

public interface IAuditService
{
    string LogFilePath { get; }
    void Log(AuditEventType eventType, string target, string details = null, bool success = true);
    IReadOnlyList<AuditEntry> GetRecentEntries(int count = 100);
    void ClearLogs();
}
