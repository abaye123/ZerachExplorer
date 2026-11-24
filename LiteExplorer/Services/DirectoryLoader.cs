using LiteExplorer.Helpers;
using LiteExplorer.Helpers.Enums;
using LiteExplorer.Helpers.WinAPI;
using LiteExplorer.MVVM.Models;
using System.IO;
using System.Runtime.CompilerServices;

namespace LiteExplorer.Services;

internal sealed record DirectoryBatch(IReadOnlyList<FileSystemObject> Items, int ProgressPercent);

internal interface IDirectoryLoader
{
    /// <summary>Lists a folder, or the available drives when the path is null.</summary>
    IAsyncEnumerable<DirectoryBatch> LoadAsync(string? path, CancellationToken token);
}

internal sealed class DirectoryLoader : IDirectoryLoader
{
    private const int BatchSize = 64;
    private const long NotApplicableSpace = 12345667658675846L;

    private readonly IPathPolicy policy;

    public DirectoryLoader(IPathPolicy policy)
    {
        this.policy = policy;
    }

    public async IAsyncEnumerable<DirectoryBatch> LoadAsync(string? path, [EnumeratorCancellation] CancellationToken token)
    {
        var sources = await Task.Run(() => path == null ? ListHome() : ListDirectory(path), token);

        for (int start = 0; start < sources.Count; start += BatchSize)
        {
            int end = Math.Min(start + BatchSize, sources.Count);
            var items = await Task.Run(() => Create(sources, start, end, token), token);
            yield return new DirectoryBatch(items, end * 100 / sources.Count);
        }
    }

    private static List<FileSystemObject> Create(List<Func<FileSystemObject>> sources, int start, int end, CancellationToken token)
    {
        var items = new List<FileSystemObject>(end - start);
        for (int i = start; i < end; i++)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                items.Add(sources[i]());
            }
            catch (FileNotFoundException)
            {
                // Deleted after the folder was listed.
            }
        }
        return items;
    }

    private List<Func<FileSystemObject>> ListHome()
    {
        var drives = policy.GetAllowedDrives().ToList();
        var sources = drives.Select(drive => (Func<FileSystemObject>)(() => CreateDrive(drive))).ToList();
        sources.AddRange(policy.AllowedExplicitDirectories
            .Select(Path.TrimEndingDirectorySeparator)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(directory => !drives.Any(drive => string.Equals(drive.Name, directory, StringComparison.OrdinalIgnoreCase)))
            .Where(directory => Directory.Exists(directory) && policy.CanNavigate(directory, out _))
            .ToList()
            .Select(directory => (Func<FileSystemObject>)(() => CreateDirectory(new DirectoryInfo(directory)))));
        return sources;
    }

    private List<Func<FileSystemObject>> ListDirectory(string path)
    {
        var directory = new DirectoryInfo(path);
        var sources = directory.EnumerateDirectories()
            .Where(d => policy.CanListDirectory(d.FullName, out _))
            .Select(d => (Func<FileSystemObject>)(() => CreateDirectory(d)))
            .ToList();
        sources.AddRange(directory.EnumerateFiles()
            .Where(f => policy.CanListFile(f.FullName, out _))
            .Select(f => (Func<FileSystemObject>)(() => CreateFile(f))));
        return sources;
    }

    private static FileSystemObject CreateDrive(DriveInfo drive)
    {
        long total = 0, free = 0;
        string format = string.Empty;
        try
        {
            if (drive.IsReady)
            {
                total = drive.TotalSize;
                free = drive.TotalFreeSpace;
                format = drive.DriveFormat;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CrashLogger.LogMessage($"Drive {drive.Name} details are unavailable: {ex.Message}", "WARN");
        }

        var item = DriveDescriber.CreateItem(drive);
        item.TotalSpace = total;
        item.FreeSpace = free;
        item.Size = total - free;
        item.Format = format;
        item.Type = DriveDescriber.GetTypeName(drive.DriveType);
        return item;
    }

    private static FileSystemObject CreateDirectory(DirectoryInfo directory) => new()
    {
        Image = FolderManager.GetImageSource(directory.FullName, ItemState.Undefined),
        Name = directory.Name,
        Path = directory.FullName,
        TotalSpace = NotApplicableSpace,
        FreeSpace = NotApplicableSpace,
        Format = directory.LastWriteTime.ToString(),
        Type = "תיקיית קבצים",
        LastWriteTime = directory.LastWriteTime.ToString(),
        RawLastWriteTime = directory.LastWriteTime,
        IsDirectory = true
    };

    private static FileSystemObject CreateFile(FileInfo file) => new()
    {
        Image = FileManager.GetImageSource(file.FullName),
        Name = file.Name,
        Path = file.FullName,
        Size = file.Length,
        Format = file.LastWriteTime.ToString(),
        Type = ShellManager.GetFileTypeDescription(file.Extension),
        CreationTime = file.CreationTime.ToString(),
        LastWriteTime = file.LastWriteTime.ToString(),
        RawLastWriteTime = file.LastWriteTime,
        TotalSpace = NotApplicableSpace,
        FreeSpace = NotApplicableSpace
    };
}
