using LiteExplorer.Infrastructure.Converters;
using LiteExplorer.MVVM.Models;
using LiteExplorer.Services;
using System.IO;
using System.Windows.Media;

namespace LiteExplorer.MVVM.ViewModels;

internal sealed class PreviewViewModel : IDisposable
{
    private readonly IPathPolicy policy;
    private CancellationTokenSource? loading;

    public PreviewViewModel(IPathPolicy policy)
    {
        this.policy = policy;
    }

    public FilePreviewInfo Info { get; } = new();

    public void Clear()
    {
        Cancel();
        Info.Clear();
    }

    /// <summary>Describes a folder or several items, when no single file is selected.</summary>
    public void ShowSummary(string name, ImageSource? icon, string summary, string hint)
    {
        Cancel();
        // Only what changed is replaced, so that a folder being loaded does not flicker.
        if (Info.Summary == null)
            Info.Clear();

        Info.Type = PreviewType.Directory;
        Info.FileName = name;
        Info.IconPreview = icon;
        Info.Summary = summary;
        Info.StatusMessage = hint;
    }

    public async Task LoadAsync(FileSystemObject? item)
    {
        Clear();
        if (item == null || string.IsNullOrEmpty(item.Path))
            return;

        Info.FileName = item.Name;
        Info.FilePath = item.Path;
        Info.LastModifiedFormatted = item.LastWriteTime;
        Info.IconPreview = item.LargeImage ?? item.Image;

        if (item.IsDirectory)
        {
            Info.Type = PreviewType.Directory;
            Info.FileSizeFormatted = item.Size > 0 ? BytesToNormalSize.FormatBytes(item.Size) : "תיקייה";
            Info.StatusMessage = "תיקייה";
            return;
        }

        Info.FileSizeFormatted = BytesToNormalSize.FormatBytes(item.Size);

        if (!policy.CanPreviewFile(item.Path, out var reason))
        {
            Info.StatusMessage = $"תצוגה מקדימה חסומה: {reason}";
            return;
        }

        if (Path.GetExtension(item.Path).ToLowerInvariant() is ".txt" or ".csv")
        {
            await LoadTextAsync(item.Path);
            return;
        }

        Info.Type = PreviewType.Document;
        Info.StatusMessage = "קובץ מסמך (דאבל קליק לפתיחה ביישום ברירת מחדל)";
    }

    public void Dispose() => Cancel();

    private async Task LoadTextAsync(string path)
    {
        var token = (loading = new CancellationTokenSource()).Token;
        Info.IsLoading = true;
        Info.Type = PreviewType.Text;

        string? text = null;
        string? error = null;
        try
        {
            text = await TextPreviewReader.ReadAsync(path, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
        }

        // Another item was selected while this one was being read.
        if (token.IsCancellationRequested)
            return;

        Info.IsLoading = false;
        if (error != null)
        {
            Info.Type = PreviewType.Document;
            Info.StatusMessage = "שגיאה בקריאת הקובץ: " + error;
            return;
        }

        Info.TextContent = text;
        Info.StatusMessage = $"מוצגים {text!.Length} תווים ראשונים";
    }

    private void Cancel()
    {
        loading?.Cancel();
        loading?.Dispose();
        loading = null;
    }
}
