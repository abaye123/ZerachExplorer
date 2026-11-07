using LiteExplorer.MVVM.ViewModels.Base;
using System.Windows.Media;

namespace LiteExplorer.MVVM.Models;

public enum PreviewType
{
    None,
    Text,
    Image,
    Document,
    Directory
}

internal class FilePreviewInfo : ViewModel
{
    private PreviewType type = PreviewType.None;
    public PreviewType Type
    {
        get => type;
        set => SetValue(ref type, value);
    }

    private string fileName;
    public string FileName
    {
        get => fileName;
        set => SetValue(ref fileName, value);
    }

    private string filePath;
    public string FilePath
    {
        get => filePath;
        set => SetValue(ref filePath, value);
    }

    private string fileSizeFormatted;
    public string FileSizeFormatted
    {
        get => fileSizeFormatted;
        set => SetValue(ref fileSizeFormatted, value);
    }

    private string lastModifiedFormatted;
    public string LastModifiedFormatted
    {
        get => lastModifiedFormatted;
        set => SetValue(ref lastModifiedFormatted, value);
    }

    private string textContent;
    public string TextContent
    {
        get => textContent;
        set => SetValue(ref textContent, value);
    }

    private ImageSource imagePreview;
    public ImageSource ImagePreview
    {
        get => imagePreview;
        set => SetValue(ref imagePreview, value);
    }

    private ImageSource iconPreview;
    public ImageSource IconPreview
    {
        get => iconPreview;
        set => SetValue(ref iconPreview, value);
    }

    private bool isLoading;
    public bool IsLoading
    {
        get => isLoading;
        set => SetValue(ref isLoading, value);
    }

    private string statusMessage = "בחר קובץ להצגת תצוגה מקדימה";
    public string StatusMessage
    {
        get => statusMessage;
        set => SetValue(ref statusMessage, value);
    }

    public void Clear()
    {
        Type = PreviewType.None;
        FileName = null;
        FilePath = null;
        FileSizeFormatted = null;
        LastModifiedFormatted = null;
        TextContent = null;
        ImagePreview = null;
        IconPreview = null;
        IsLoading = false;
        StatusMessage = "בחר קובץ להצגת תצוגה מקדימה";
    }
}
