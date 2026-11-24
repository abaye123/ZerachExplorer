using LiteExplorer.Helpers;
using LiteExplorer.MVVM.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace LiteExplorer.Services;

public class ConfigurationService : IConfigurationService
{
    public const int MinimumPasswordLength = 8;
    private const int FreeAttempts = 5;
    private static readonly TimeSpan BaseLockout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaximumLockout = TimeSpan.FromMinutes(15);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string configFilePath;
    private readonly bool isProtected;
    private readonly Func<DateTime> utcNow;
    private AppConfiguration? currentConfig;
    private int failedAttempts;
    private DateTime lockedUntil;

    public ConfigurationService(string? customConfigPath = null, Func<DateTime>? clock = null)
    {
        isProtected = string.IsNullOrWhiteSpace(customConfigPath);
        configFilePath = isProtected ? ProtectedConfigurationStore.FilePath : customConfigPath!;
        utcNow = clock ?? (() => DateTime.UtcNow);
    }

    public string ConfigFilePath => configFilePath;

    public AppConfiguration Configuration => currentConfig ?? Load();

    /// <summary>
    /// The stored file is damaged or untrusted, so everything stays denied until an administrator saves again.
    /// </summary>
    public bool IsLocked { get; private set; }

    public bool HasPassword =>
        !string.IsNullOrEmpty(Configuration.AdminPasswordHash) && !string.IsNullOrEmpty(Configuration.AdminPasswordSalt);

    public AppConfiguration Load()
    {
        IsLocked = false;
        try
        {
            if (!File.Exists(configFilePath))
                return currentConfig = new AppConfiguration();

            if (isProtected && !ProtectedConfigurationStore.IsTrusted(configFilePath))
                throw new InvalidDataException("The configuration file is not owned by an administrator.");

            return currentConfig = Parse(File.ReadAllBytes(configFilePath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            CrashLogger.LogException(ex, "ConfigurationService.Load");
            IsLocked = true;
            return currentConfig = new AppConfiguration
            {
                Profile = KioskProfile.Custom,
                Permissions = PathPermissions.CreateDenied(),
                AllowedExtensions = new List<string>()
            };
        }
    }

    public bool Save(AppConfiguration configuration, out string? error)
    {
        error = null;
        if (configuration == null)
        {
            error = "תצורה ריקה.";
            return false;
        }

        try
        {
            byte[] content = JsonSerializer.SerializeToUtf8Bytes(configuration, JsonOptions);

            if (isProtected)
            {
                if (!ProtectedConfigurationStore.Save(content, out error))
                    return false;
            }
            else
            {
                string? directory = Path.GetDirectoryName(configFilePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllBytes(configFilePath, content);
            }

            currentConfig = Parse(content);
            IsLocked = false;
            return true;
        }
        catch (Exception ex)
        {
            CrashLogger.LogException(ex, "ConfigurationService.Save");
            error = $"שגיאה בשמירת התצורה: {ex.Message}";
            return false;
        }
    }

    public AppConfiguration CreateEditableCopy()
    {
        // A locked configuration denies everything; repairing it starts from the normal defaults.
        return IsLocked ? new AppConfiguration() : Parse(JsonSerializer.SerializeToUtf8Bytes(Configuration, JsonOptions));
    }

    public bool VerifyPassword(string password, out TimeSpan retryAfter)
    {
        retryAfter = lockedUntil - utcNow();
        if (retryAfter > TimeSpan.Zero)
            return false;

        retryAfter = TimeSpan.Zero;
        if (!HasPassword)
            return false;

        if (SecurityHelper.VerifyPassword(password, Configuration.AdminPasswordHash, Configuration.AdminPasswordSalt))
        {
            failedAttempts = 0;
            return true;
        }

        failedAttempts++;
        if (failedAttempts >= FreeAttempts)
        {
            double factor = Math.Pow(2, Math.Min(failedAttempts - FreeAttempts, 10));
            retryAfter = TimeSpan.FromTicks(Math.Min((long)(BaseLockout.Ticks * factor), MaximumLockout.Ticks));
            lockedUntil = utcNow() + retryAfter;
        }
        return false;
    }

    public bool VerifyPassword(string password) => VerifyPassword(password, out _);

    public void SetPassword(AppConfiguration configuration, string newPassword)
    {
        SecurityHelper.HashPassword(newPassword, out string hash, out string salt);
        configuration.AdminPasswordHash = hash;
        configuration.AdminPasswordSalt = salt;
    }

    public void ApplyToPolicy(IPathPolicy policy)
    {
        if (policy == null) return;
        var config = Configuration;

        policy.Profile = config.Profile;
        policy.Permissions = config.Permissions;
        Replace(policy.AllowedDriveRoots, config.AllowedDriveRoots);
        Replace(policy.AllowedVolumeSerialNumbers, config.AllowedVolumeSerialNumbers);
        Replace(policy.AllowedExplicitDirectories, config.AllowedExplicitDirectories);
        Replace(policy.BlockedVolumeLabels, config.BlockedVolumeLabels);
        Replace(policy.BlockedDriveRoots, config.BlockedDriveRoots);
        Replace(policy.BlockedVolumeSerialNumbers, config.BlockedVolumeSerialNumbers);
        policy.SetAllowedExtensions(config.AllowedExtensions);
    }

    internal static AppConfiguration Parse(byte[] content)
    {
        var config = JsonSerializer.Deserialize<AppConfiguration>(content, JsonOptions)
                     ?? throw new InvalidDataException("The configuration file is empty.");

        bool hasNull = config.Permissions == null || new[]
        {
            config.AllowedExtensions, config.AllowedDriveRoots, config.AllowedVolumeSerialNumbers,
            config.AllowedExplicitDirectories, config.BlockedVolumeLabels, config.BlockedDriveRoots,
            config.BlockedVolumeSerialNumbers
        }.Any(list => list == null);

        if (hasNull)
            throw new InvalidDataException("The configuration file has missing values.");

        config.LogRetentionDays = Math.Clamp(config.LogRetentionDays, 1, AppConfiguration.MaximumLogRetentionDays);
        config.LogMaxSizeMegabytes = Math.Clamp(config.LogMaxSizeMegabytes, 1, AppConfiguration.MaximumLogMaxSizeMegabytes);
        return config;
    }

    private static void Replace(ISet<string> target, IEnumerable<string> values)
    {
        target.Clear();
        target.UnionWith(values);
    }
}
