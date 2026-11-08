using System;

namespace LiteExplorer.MVVM.Models;

public class AuditEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public AuditEventType EventType { get; set; }
    public string Target { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public bool Success { get; set; } = true;

    public override string ToString()
    {
        string status = Success ? "SUCCESS" : "BLOCKED";
        return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{EventType}] [{status}] Target: {Target} | Details: {Details}";
    }

    public static AuditEntry Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        try
        {
            var entry = new AuditEntry();
            var parts = line.Split("] [");
            if (parts.Length >= 3)
            {
                string timeStr = parts[0].TrimStart('[');
                if (DateTime.TryParse(timeStr, out var time))
                {
                    entry.Timestamp = time;
                }

                if (Enum.TryParse<AuditEventType>(parts[1], out var ev))
                {
                    entry.EventType = ev;
                }

                string rest = parts[2];
                int endBracket = rest.IndexOf(']');
                if (endBracket > 0)
                {
                    entry.Success = rest.Substring(0, endBracket).Trim() == "SUCCESS";
                    string payload = rest.Substring(endBracket + 1).Trim();

                    int targetIdx = payload.IndexOf("Target:");
                    int detailsIdx = payload.IndexOf("| Details:");

                    if (targetIdx >= 0 && detailsIdx > targetIdx)
                    {
                        entry.Target = payload.Substring(targetIdx + 7, detailsIdx - (targetIdx + 7)).Trim();
                        entry.Details = payload.Substring(detailsIdx + 10).Trim();
                    }
                    else if (targetIdx >= 0)
                    {
                        entry.Target = payload.Substring(targetIdx + 7).Trim();
                    }
                }
            }
            return entry;
        }
        catch
        {
            return null;
        }
    }
}
