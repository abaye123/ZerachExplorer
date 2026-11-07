using LiteExplorer.Helpers;
using LiteExplorer.MVVM.Models;
using System;
using System.IO;
using System.Text.Json;

namespace LiteExplorer.Services;

public class ConfigurationService : IConfigurationService
{
    public static ConfigurationService Instance { get; } = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string configFilePath;
    private AppConfiguration currentConfig;

    public string ConfigFilePath => configFilePath;
    public AppConfiguration Configuration => currentConfig ??= Load();

    public ConfigurationService(string customConfigPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customConfigPath))
        {
            configFilePath = customConfigPath;
        }
        else
        {
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            configFilePath = Path.Combine(programData, "ZerachExplorer", "config.json");
        }
    }

    public AppConfiguration Load()
    {
        try
        {
            if (File.Exists(configFilePath))
            {
                string json = File.ReadAllText(configFilePath);
                var loaded = JsonSerializer.Deserialize<AppConfiguration>(json, JsonOptions);
                if (loaded != null)
                {
                    currentConfig = loaded;
                    return currentConfig;
                }
            }
        }
        catch (Exception ex)
        {
            CrashLogger.LogException(ex, "ConfigurationService.Load");
        }

        currentConfig = CreateDefaultConfiguration();
        try
        {
            Save(currentConfig, out _);
        }
        catch
        {
        }

        return currentConfig;
    }

    public bool Save(AppConfiguration configuration, out string error)
    {
        error = null;
        if (configuration == null)
        {
            error = "תצורה ריקה.";
            return false;
        }

        try
        {
            string dir = Path.GetDirectoryName(configFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(configuration, JsonOptions);
            File.WriteAllText(configFilePath, json);
            currentConfig = configuration;
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            error = "אין הרשאות מנהל מערכת לשמירת קובץ התצורה ב-ProgramData.";
            CrashLogger.LogException(ex, "ConfigurationService.Save.Unauthorized");
            return false;
        }
        catch (Exception ex)
        {
            error = $"שגיאה בשמירת התצורה: {ex.Message}";
            CrashLogger.LogException(ex, "ConfigurationService.Save");
            return false;
        }
    }

    public bool VerifyPassword(string password)
    {
        var config = Configuration;
        if (string.IsNullOrEmpty(config.AdminPasswordHash) || string.IsNullOrEmpty(config.AdminPasswordSalt))
        {
            return true;
        }

        return SecurityHelper.VerifyPassword(password, config.AdminPasswordHash, config.AdminPasswordSalt);
    }

    public void ChangePassword(string newPassword)
    {
        var config = Configuration;
        SecurityHelper.HashPassword(newPassword, out string hash, out string salt);
        config.AdminPasswordHash = hash;
        config.AdminPasswordSalt = salt;
    }

    public void ApplyToPolicy(IPathPolicy policy)
    {
        if (policy == null) return;
        var config = Configuration;

        policy.Profile = config.Profile;
        policy.Permissions = config.Permissions ?? PathPermissions.CreateStandard();

        policy.AllowedDriveRoots.Clear();
        foreach (var d in config.AllowedDriveRoots) policy.AllowedDriveRoots.Add(d);

        policy.AllowedVolumeSerialNumbers.Clear();
        foreach (var s in config.AllowedVolumeSerialNumbers) policy.AllowedVolumeSerialNumbers.Add(s);

        policy.AllowedExplicitDirectories.Clear();
        foreach (var dir in config.AllowedExplicitDirectories) policy.AllowedExplicitDirectories.Add(dir);

        policy.BlockedVolumeLabels.Clear();
        foreach (var l in config.BlockedVolumeLabels) policy.BlockedVolumeLabels.Add(l);

        policy.BlockedVolumeSerialNumbers.Clear();
        foreach (var s in config.BlockedVolumeSerialNumbers) policy.BlockedVolumeSerialNumbers.Add(s);

        policy.SetAllowedExtensions(config.AllowedExtensions);
    }

    private static AppConfiguration CreateDefaultConfiguration()
    {
        var config = new AppConfiguration();
        SecurityHelper.HashPassword("admin123", out string hash, out string salt);
        config.AdminPasswordHash = hash;
        config.AdminPasswordSalt = salt;
        return config;
    }
}
