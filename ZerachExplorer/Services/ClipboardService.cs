using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace ZerachExplorer.Services;

public interface IClipboardService
{
    bool HasFiles();
    bool Write(IReadOnlyCollection<string> paths, bool isCut);
    IReadOnlyList<string> Read(out bool isCut);
    void Clear();
}

/// <summary>
/// Exchanges files with Windows Explorer. Any call can fail while another process holds the clipboard.
/// </summary>
public class ClipboardService : IClipboardService
{
    private const string DropEffectFormat = "Preferred DropEffect";

    public bool HasFiles()
    {
        try
        {
            return Clipboard.ContainsFileDropList();
        }
        catch (ExternalException)
        {
            return false;
        }
    }

    public bool Write(IReadOnlyCollection<string> paths, bool isCut)
    {
        var files = new StringCollection();
        files.AddRange(paths.ToArray());

        var data = new DataObject();
        data.SetFileDropList(files);
        data.SetData(DropEffectFormat, new MemoryStream(BitConverter.GetBytes((int)(isCut ? DragDropEffects.Move : DragDropEffects.Copy))));

        try
        {
            Clipboard.SetDataObject(data, copy: true);
            return true;
        }
        catch (ExternalException ex)
        {
            CrashLogger.LogException(ex, "Clipboard write failed");
            return false;
        }
    }

    public IReadOnlyList<string> Read(out bool isCut)
    {
        isCut = false;
        try
        {
            var data = Clipboard.GetDataObject();
            if (data?.GetData(DataFormats.FileDrop) is not string[] files)
                return Array.Empty<string>();

            if (data.GetData(DropEffectFormat) is MemoryStream { Length: >= sizeof(int) } stream)
            {
                var effect = (DragDropEffects)BitConverter.ToInt32(stream.ToArray(), 0);
                isCut = (effect & DragDropEffects.Move) != 0 && (effect & DragDropEffects.Copy) == 0;
            }

            return files.Where(file => !string.IsNullOrWhiteSpace(file)).ToList();
        }
        catch (ExternalException ex)
        {
            CrashLogger.LogException(ex, "Clipboard read failed");
            return Array.Empty<string>();
        }
    }

    public void Clear()
    {
        try
        {
            Clipboard.Clear();
        }
        catch (ExternalException ex)
        {
            CrashLogger.LogException(ex, "Clipboard clear failed");
        }
    }
}
