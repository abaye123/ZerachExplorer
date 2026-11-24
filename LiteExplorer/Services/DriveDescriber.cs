using LiteExplorer.Helpers;
using LiteExplorer.Helpers.Enums;
using LiteExplorer.MVVM.Models;
using System.IO;

namespace LiteExplorer.Services;

internal static class DriveDescriber
{
    public static string GetLabel(DriveInfo drive)
    {
        try
        {
            string label = drive.IsReady && !string.IsNullOrEmpty(drive.VolumeLabel) ? drive.VolumeLabel : "דיסק מקומי";
            return $"{label} ({drive.Name})";
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
        Image = FolderManager.GetImageSource(drive.RootDirectory.FullName, ItemState.Undefined),
        Name = GetLabel(drive),
        Path = drive.Name,
        IsDirectory = true,
        IsDrive = true,
        IsRemovableDrive = drive.DriveType == DriveType.Removable
    };
}
