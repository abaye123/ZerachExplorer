using ZerachExplorer.MVVM.Models;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace ZerachExplorer.Services;

public class SessionService : ISessionService
{
    public static SessionService Instance { get; } = new(AuditService.Instance, ClearClipboard, Path.Combine(Path.GetTempPath(), "ZerachExplorer"));

    private readonly IAuditService audit;
    private readonly Action clearClipboard;
    private readonly string tempDirectory;

    internal SessionService(IAuditService audit, Action clearClipboard, string tempDirectory)
    {
        this.audit = audit;
        this.clearClipboard = clearClipboard;
        this.tempDirectory = tempDirectory;
    }

    public event EventHandler? SessionReset;

    public void ResetSession()
    {
        WipeClipboard();
        CleanTempFiles();
        audit.Log(AuditEventType.SessionReset, "Session", "User session and temporary buffers reset", true);
        SessionReset?.Invoke(this, EventArgs.Empty);
    }

    public void WipeClipboard()
    {
        try
        {
            clearClipboard();
        }
        catch (ExternalException ex)
        {
            // Another process holds the clipboard.
            CrashLogger.LogException(ex, "Clipboard wipe failed");
        }
    }

    public void CleanTempFiles()
    {
        if (!Directory.Exists(tempDirectory))
            return;

        foreach (var file in Directory.GetFiles(tempDirectory))
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                CrashLogger.LogMessage($"Temporary file {file} was not removed: {ex.Message}", "WARN");
            }
        }
    }

    private static void ClearClipboard()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null)
            dispatcher.Invoke(Clipboard.Clear);
    }
}
