using System.IO;
using System.Windows.Threading;

namespace ZerachExplorer.Services;

/// <summary>Reports changes in one folder, grouped so that a burst of changes causes a single refresh.</summary>
internal sealed class FolderWatcher : IDisposable
{
    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer debounce;
    private FileSystemWatcher? watcher;

    public FolderWatcher(Action onChanged)
    {
        debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        debounce.Tick += (_, _) =>
        {
            debounce.Stop();
            onChanged();
        };
    }

    public void Start(string? path)
    {
        Stop();

        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return;

        try
        {
            watcher = new FileSystemWatcher(path)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size
            };
            watcher.Created += OnChanged;
            watcher.Deleted += OnChanged;
            watcher.Renamed += OnChanged;
            watcher.Changed += OnChanged;
            watcher.Error += OnError;
            watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            CrashLogger.LogMessage($"Folder watcher could not start on {path}: {ex.Message}", "INFO");
            Stop();
        }
    }

    public void Stop()
    {
        debounce.Stop();
        watcher?.Dispose();
        watcher = null;
    }

    public void Dispose() => Stop();

    private void OnChanged(object sender, FileSystemEventArgs e) => dispatcher.InvokeAsync(() =>
    {
        // A queued event from a folder that is no longer shown.
        if (!ReferenceEquals(sender, watcher)) return;

        debounce.Stop();
        debounce.Start();
    });

    private void OnError(object sender, ErrorEventArgs e) => dispatcher.InvokeAsync(() =>
    {
        if (!ReferenceEquals(sender, watcher)) return;

        CrashLogger.LogException(e.GetException(), "Folder watcher stopped");
        Stop();
    });
}
