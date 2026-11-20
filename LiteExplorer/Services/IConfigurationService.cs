using LiteExplorer.MVVM.Models;

namespace LiteExplorer.Services;

public interface IConfigurationService
{
    string ConfigFilePath { get; }
    AppConfiguration Configuration { get; }
    AppConfiguration Load();
    bool Save(AppConfiguration configuration, out string error);
    bool VerifyPassword(string password);
    void ChangePassword(string newPassword);
    void ApplyToPolicy(IPathPolicy policy);
}
