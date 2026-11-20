using LiteExplorer.MVVM.ViewModels.Base;
using System.Windows.Media;

namespace LiteExplorer.MVVM.Models;

internal class FileSystemObject : ViewModel
{
    private ImageSource image;
    public ImageSource Image
    {
        get => image;
        set => SetValue(ref image, value);
    }

    private string name;
    public string Name
    {
        get => name;
        set => SetValue(ref name, value);
    }

    private string path;
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

    private string format;
    public string Format
    {
        get => format;
        set => SetValue(ref format, value);
    }

    private string type;
    public string Type
    {
        get => type;
        set => SetValue(ref type, value);
    }

    private string creationTime;
    public string CreationTime
    {
        get => creationTime;
        set => SetValue(ref creationTime, value);
    }

    private string lastWriteTime;
    public string LastWriteTime
    {
        get => lastWriteTime;
        set => SetValue(ref lastWriteTime, value);
    }

    private bool isDirectory;
    public bool IsDirectory
    {
        get => isDirectory;
        set => SetValue(ref isDirectory, value);
    }

    private bool isCalculatingSize;
    public bool IsCalculatingSize
    {
        get => isCalculatingSize;
        set => SetValue(ref isCalculatingSize, value);
    }
}
