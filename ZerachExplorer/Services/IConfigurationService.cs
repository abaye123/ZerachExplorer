using ZerachExplorer.MVVM.Models;

namespace ZerachExplorer.Services;

public interface IConfigurationService
{
    string ConfigFilePath { get; }
    AppConfiguration Configuration { get; }
    AppConfiguration Load();
    bool Save(AppConfiguration configuration, out string? error);
    bool VerifyPassword(string password);
    bool VerifyPassword(string password, out TimeSpan retryAfter);
    bool HasPassword { get; }
    bool IsLocked { get; }
    AppConfiguration CreateEditableCopy();
    void SetPassword(AppConfiguration configuration, string newPassword);
    void ApplyToPolicy(IPathPolicy policy);
}
