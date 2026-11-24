namespace LiteExplorer.Services;

/// <summary>
/// Shared between the thread that does the work and the window that shows it:
/// the worker reports what it has done, the window can pause or cancel it.
/// </summary>
public sealed class OperationProgress : IDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly ManualResetEventSlim running = new(true);
    private long totalBytes;
    private long finishedBytes;
    private long currentFileBytes;
    private int totalItems;
    private int finishedItems;
    private volatile string currentName = string.Empty;

    public long TotalBytes => Interlocked.Read(ref totalBytes);
    public long DoneBytes => Math.Min(TotalBytes, Interlocked.Read(ref finishedBytes) + Interlocked.Read(ref currentFileBytes));
    public int TotalItems => Volatile.Read(ref totalItems);
    public int DoneItems => Volatile.Read(ref finishedItems);
    public string CurrentName => currentName;
    public bool IsPaused => !running.IsSet;
    public bool IsCancelled => cancellation.IsCancellationRequested;

    public void Pause() => running.Reset();

    public void Resume() => running.Set();

    public void Cancel()
    {
        cancellation.Cancel();
        // A paused worker has to wake up to notice.
        running.Set();
    }

    /// <summary>Waits while paused; throws when cancelled.</summary>
    public void Checkpoint()
    {
        cancellation.Token.ThrowIfCancellationRequested();
        running.Wait(cancellation.Token);
        cancellation.Token.ThrowIfCancellationRequested();
    }

    public void AddTotal(int items, long bytes)
    {
        Interlocked.Add(ref totalItems, items);
        Interlocked.Add(ref totalBytes, bytes);
    }

    public void Begin(string name)
    {
        currentName = name;
        Interlocked.Exchange(ref currentFileBytes, 0);
    }

    public void ReportFileBytes(long bytes) => Interlocked.Exchange(ref currentFileBytes, bytes);

    /// <summary>The item is dealt with, whether it succeeded, failed or was skipped.</summary>
    public void Finish(long bytes)
    {
        Interlocked.Exchange(ref currentFileBytes, 0);
        Interlocked.Add(ref finishedBytes, bytes);
        Interlocked.Increment(ref finishedItems);
    }

    public void Dispose()
    {
        cancellation.Dispose();
        running.Dispose();
    }
}
