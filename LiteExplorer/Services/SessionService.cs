using System;
using System.IO;
using System.Threading;
using System.Windows;
using LiteExplorer.MVVM.Models;

namespace LiteExplorer.Services;

public class SessionService : ISessionService
{
    public static SessionService Instance { get; } = new();

    public event EventHandler SessionReset;

    public void ResetSession()
    {
        WipeClipboard();
        CleanTempFiles();

        AuditService.Instance.Log(AuditEventType.SessionReset, "Session", "User session and temporary buffers reset", true);

        SessionReset?.Invoke(this, EventArgs.Empty);
    }

    public void WipeClipboard()
    {
        try
        {
            if (Application.Current != null && Application.Current.Dispatcher != null)
            {
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    Clipboard.Clear();
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() => Clipboard.Clear());
                }
            }
            else
            {
                var staThread = new Thread(() =>
                {
                    try { Clipboard.Clear(); } catch { }
                });
                staThread.SetApartmentState(ApartmentState.STA);
                staThread.Start();
                staThread.Join(500);
            }
        }
        catch
        {
            // Suppress clipboard wipe errors
        }
    }

    public void CleanTempFiles()
    {
        try
        {
            string appTempDir = Path.Combine(Path.GetTempPath(), "ZerachExplorer");
            if (Directory.Exists(appTempDir))
            {
                foreach (var file in Directory.GetFiles(appTempDir))
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }
        catch
        {
            // Suppress temp file deletion errors
        }
    }
}
