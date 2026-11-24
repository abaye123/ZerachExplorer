using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.Text.Json;

namespace LiteExplorer.Services;

/// <summary>Display preferences of the current user. They carry no permissions, so a lost file only restores defaults.</summary>
public sealed class ViewSettings : ObservableObject
{
    private readonly string path;
    private bool showCheckBoxes;

    private sealed record Stored(bool ShowCheckBoxes);

    public ViewSettings()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZerachExplorer", "view.json"))
    {
    }

    internal ViewSettings(string path)
    {
        this.path = path;
        showCheckBoxes = Load().ShowCheckBoxes;
    }

    public bool ShowCheckBoxes
    {
        get => showCheckBoxes;
        set
        {
            if (SetProperty(ref showCheckBoxes, value))
                Save();
        }
    }

    private Stored Load()
    {
        var defaults = new Stored(ShowCheckBoxes: true);
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<Stored>(File.ReadAllText(path)) ?? defaults : defaults;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            CrashLogger.LogException(ex, "Loading view settings");
            return defaults;
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(new Stored(ShowCheckBoxes)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CrashLogger.LogException(ex, "Saving view settings");
        }
    }
}
