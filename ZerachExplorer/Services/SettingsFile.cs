using System.IO;
using System.Text.Json;
using ZerachExplorer.MVVM.Models;

namespace ZerachExplorer.Services;

/// <summary>Settings carried from one computer to another. The format is that of the configuration file itself.</summary>
public static class SettingsFile
{
    public const int MaxBytes = 1024 * 1024;

    /// <param name="includePassword">The password is stored only as a salted hash, but a weak one can still be guessed from it.</param>
    public static byte[] Export(AppConfiguration configuration, bool includePassword)
    {
        var copy = ConfigurationService.Parse(ConfigurationService.Serialize(configuration));
        if (!includePassword)
            copy.AdminPasswordHash = copy.AdminPasswordSalt = string.Empty;
        return ConfigurationService.Serialize(copy);
    }

    /// <summary>The imported settings; without a password in the file, the current one is kept.</summary>
    /// <exception cref="InvalidDataException">Not a settings file of this application.</exception>
    public static AppConfiguration Import(byte[] content, AppConfiguration current)
    {
        AppConfiguration imported;
        try
        {
            // An unrelated file would otherwise be accepted as "all defaults".
            using (var document = JsonDocument.Parse(content))
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object ||
                    !document.RootElement.TryGetProperty(nameof(AppConfiguration.Permissions), out _) ||
                    !document.RootElement.TryGetProperty(nameof(AppConfiguration.AllowedExtensions), out _))
                    throw new InvalidDataException("Not a settings file.");
            }

            imported = ConfigurationService.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Not a settings file.", ex);
        }

        if (!HasPassword(imported))
        {
            imported.AdminPasswordHash = current.AdminPasswordHash;
            imported.AdminPasswordSalt = current.AdminPasswordSalt;
        }
        return imported;
    }

    public static bool HasPassword(AppConfiguration configuration) =>
        !string.IsNullOrEmpty(configuration.AdminPasswordHash) && !string.IsNullOrEmpty(configuration.AdminPasswordSalt);

    public static byte[] Read(string path)
    {
        if (new FileInfo(path).Length > MaxBytes)
            throw new InvalidDataException("The file is too large.");
        return File.ReadAllBytes(path);
    }
}
