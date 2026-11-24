using System.IO;

namespace ZerachExplorer.Services;

public sealed record FolderContents(bool HasFolders, int Files, int Folders, int Hidden);

public static class FolderInspector
{
    private static readonly EnumerationOptions Everything = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = 0
    };

    public static Task<long> CalculateSizeAsync(string directory, CancellationToken token = default) => Task.Run(() =>
    {
        long size = 0;
        foreach (var file in new DirectoryInfo(directory).EnumerateFiles("*", WithoutLinks()))
        {
            token.ThrowIfCancellationRequested();
            size += file.Length;
        }
        return size;
    }, token);

    public static FolderContents CountContents(IEnumerable<string> directories, OperationProgress progress)
    {
        int files = 0, folders = 0, hidden = 0;
        bool any = false;
        foreach (var directory in directories)
        {
            any = true;
            progress.Begin(Path.GetFileName(directory));
            try
            {
                foreach (var info in new DirectoryInfo(directory).EnumerateFileSystemInfos("*", Everything))
                {
                    progress.Checkpoint();
                    if ((info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) hidden++;
                    if ((info.Attributes & FileAttributes.Directory) != 0) folders++; else files++;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                CrashLogger.LogException(ex, $"Counting contents of {directory}");
            }
        }
        return new FolderContents(any, files, folders, hidden);
    }

    // Following links could count other folders, or never finish.
    private static EnumerationOptions WithoutLinks() => new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint
    };
}
