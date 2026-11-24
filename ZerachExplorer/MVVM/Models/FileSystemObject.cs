using ZerachExplorer.Infrastructure.Converters;
using ZerachExplorer.MVVM.ViewModels.Base;
using System;
using System.Windows.Media;

namespace ZerachExplorer.MVVM.Models;

internal class FileSystemObject : ViewModel
{
    private ImageSource? image;
    public ImageSource? Image
    {
        get => image;
        set => SetValue(ref image, value);
    }

    private ImageSource? largeImage;
    public ImageSource? LargeImage
    {
        get => largeImage;
        set => SetValue(ref largeImage, value);
    }

    private string name = string.Empty;
    public string Name
    {
        get => name;
        set => SetValue(ref name, value);
    }

    private string path = string.Empty;
    public string Path
    {
        get => path;
        set => SetValue(ref path, value);
    }

    private long size;
    public long Size
    {
        get => size;
        set => SetValue(ref size, value);
    }

    private long totalSpace;
    public long TotalSpace
    {
        get => totalSpace;
        set => SetValue(ref totalSpace, value);
    }

    private long freeSpace;
    public long FreeSpace
    {
        get => freeSpace;
        set => SetValue(ref freeSpace, value);
    }

    private string? format;
    public string? Format
    {
        get => format;
        set => SetValue(ref format, value);
    }

    private string? type;
    public string? Type
    {
        get => type;
        set => SetValue(ref type, value);
    }

    private string? creationTime;
    public string? CreationTime
    {
        get => creationTime;
        set => SetValue(ref creationTime, value);
    }

    private string? lastWriteTime;
    public string? LastWriteTime
    {
        get => lastWriteTime;
        set => SetValue(ref lastWriteTime, value);
    }

    private DateTime? rawLastWriteTime;
    public DateTime? RawLastWriteTime
    {
        get => rawLastWriteTime;
        set => SetValue(ref rawLastWriteTime, value);
    }

    private bool isDirectory;
    public bool IsDirectory
    {
        get => isDirectory;
        set => SetValue(ref isDirectory, value);
    }

    private bool isDrive;
    public bool IsDrive
    {
        get => isDrive;
        set => SetValue(ref isDrive, value);
    }

    private bool isRemovableDrive;
    public bool IsRemovableDrive
    {
        get => isRemovableDrive;
        set => SetValue(ref isRemovableDrive, value);
    }

    private bool isCalculatingSize;
    public bool IsCalculatingSize
    {
        get => isCalculatingSize;
        set => SetValue(ref isCalculatingSize, value);
    }

    public string SpaceText => TotalSpace <= 0
        ? string.Empty
        : $"\u200E{BytesToNormalSize.FormatBytes(FreeSpace)}\u200E פנויים מתוך \u200E{BytesToNormalSize.FormatBytes(TotalSpace)}\u200E";

    public double UsedPercent => TotalSpace <= 0 ? 0 : 100.0 * (TotalSpace - FreeSpace) / TotalSpace;

    public bool ShowSpace => IsDrive && TotalSpace > 0;

    /// <summary>Takes over newer details of the same item; returns whether anything changed.</summary>
    public bool UpdateFrom(FileSystemObject newer)
    {
        bool changed = Name != newer.Name || Path != newer.Path || Type != newer.Type || Format != newer.Format ||
                       RawLastWriteTime != newer.RawLastWriteTime || FreeSpace != newer.FreeSpace || TotalSpace != newer.TotalSpace ||
                       (!IsPlainFolder && Size != newer.Size);
        if (!changed)
            return false;

        Name = newer.Name;
        Path = newer.Path;
        Type = newer.Type;
        Format = newer.Format;
        Image = newer.Image;
        LargeImage = newer.LargeImage;
        CreationTime = newer.CreationTime;
        LastWriteTime = newer.LastWriteTime;
        RawLastWriteTime = newer.RawLastWriteTime;
        FreeSpace = newer.FreeSpace;
        TotalSpace = newer.TotalSpace;
        OnPropertyChanged(nameof(SpaceText));
        OnPropertyChanged(nameof(UsedPercent));
        OnPropertyChanged(nameof(ShowSpace));
        if (!IsPlainFolder)
            Size = newer.Size;
        return true;
    }

    // The size of a folder is only known after the user asks for it, so a refresh must not erase it.
    private bool IsPlainFolder => IsDirectory && !IsDrive;
}
