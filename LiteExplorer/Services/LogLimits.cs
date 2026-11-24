using LiteExplorer.MVVM.Models;

namespace LiteExplorer.Services;

public static class LogLimits
{
    public static void Apply(AppConfiguration configuration)
    {
        AuditService.Instance.SetLimits(configuration.LogRetentionDays, configuration.LogMaxSizeMegabytes);
        CrashLogger.SetRetention(configuration.LogRetentionDays);
    }
}
