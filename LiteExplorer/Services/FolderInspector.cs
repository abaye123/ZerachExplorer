using System.IO;

namespace LiteExplorer.Services;

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

    /// <summary>A short summary for the delete confirmation; empty when there are no folders.</summary>
    public static Task<string> DescribeContentsAsync(IReadOnlyCollection<string> directories) => Task.Run(() =>
    {
        if (directories.Count == 0)
            return string.Empty;

        int files = 0, folders = 0, hidden = 0;
        foreach (var directory in directories)
        {
            try
            {
                foreach (var info in new DirectoryInfo(directory).EnumerateFileSystemInfos("*", Everything))
                {
                    if ((info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) hidden++;
                    if ((info.Attributes & FileAttributes.Directory) != 0) folders++; else files++;
                }
            }
            catch (IOException ex)
            {
                CrashLogger.LogException(ex, $"Counting contents of {directory}");
            }
        }

        string text = $"תוכן התיקיות: {files} קבצים, {folders} תתי-תיקיות.\n";
        if (hidden > 0)
            text += $"שים לב: כולל {hidden} פריטים מוסתרים או פריטי מערכת.\n";
        return text + "\n";
    });

    // Following links could count other folders, or never finish.
    private static EnumerationOptions WithoutLinks() => new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint
    };
}
