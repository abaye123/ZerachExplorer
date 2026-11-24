using ZerachExplorer.Helpers.WinAPI;
using ZerachExplorer.MVVM.Models;
using System.IO;

namespace ZerachExplorer.Services;

internal static class DriveDescriber
{
    public static string GetLabel(DriveInfo drive)
    {
        try
        {
            string label = drive.IsReady && !string.IsNullOrEmpty(drive.VolumeLabel) ? drive.VolumeLabel : "דיסק מקומי";
            // The marks keep "(D:)" intact inside right-to-left text.
            return $"{label} (\u200E{drive.Name.TrimEnd('\\')}\u200E)";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return drive.Name;
        }
    }

    public static string GetLabel(string root)
    {
        try
        {
            return GetLabel(new DriveInfo(root));
        }
        catch (ArgumentException)
        {
            return root;
        }
    }

    public static string GetTypeName(DriveType type) => type switch
    {
        DriveType.Fixed => "דיסק מקומי",
        DriveType.Removable => "דיסק נשלף (USB)",
        _ => type.ToString()
    };

    public static bool IsRemovable(string path)
    {
        try
        {
            string? root = Path.GetPathRoot(path);
            return !string.IsNullOrEmpty(root) && new DriveInfo(root).DriveType == DriveType.Removable;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static FileSystemObject CreateItem(DriveInfo drive) => new()
    {
        Image = ShellIcons.ForLocation(drive.Name).Small,
        LargeImage = ShellIcons.ForLocation(drive.Name).Large,
        Name = GetLabel(drive),
        Path = drive.Name,
        IsDirectory = true,
        IsDrive = true,
        IsRemovableDrive = drive.DriveType == DriveType.Removable
    };
}
