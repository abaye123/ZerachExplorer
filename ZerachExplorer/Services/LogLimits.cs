using ZerachExplorer.MVVM.Models;

namespace ZerachExplorer.Services;

public static class LogLimits
{
    public static void Apply(AppConfiguration configuration)
    {
        AuditService.Instance.SetLimits(configuration.LogRetentionDays, configuration.LogMaxSizeMegabytes);
        CrashLogger.SetRetention(configuration.LogRetentionDays);
    }
}
