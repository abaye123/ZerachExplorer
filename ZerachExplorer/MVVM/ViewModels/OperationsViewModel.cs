using ZerachExplorer.Infrastructure.Commands;
using ZerachExplorer.MVVM.ViewModels.Base;
using ZerachExplorer.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;

namespace ZerachExplorer.MVVM.ViewModels;

/// <summary>Runs file operations in the background and shows those that take noticeable time.</summary>
internal sealed class OperationsViewModel : ViewModel
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);

    private readonly TimeSpan showDelay;
    private readonly List<(FileOperation Operation, Task Task)> running = new();
    private DispatcherTimer? timer;

    public OperationsViewModel()
        : this(TimeSpan.FromSeconds(1))
    {
    }

    internal OperationsViewModel(TimeSpan showDelay)
    {
        this.showDelay = showDelay;
        ShowCmd = new ActionCommand(_ => ShowRequested?.Invoke());
    }

    /// <summary>Operations that have been running long enough to be shown.</summary>
    public ObservableCollection<FileOperation> Visible { get; } = new();

    public ICommand ShowCmd { get; }

    public event Action? ShowRequested;

    public bool HasActive => running.Count > 0;

    public bool HasVisible => Visible.Count > 0;

    public string StatusText => Visible.Count switch
    {
        0 => string.Empty,
        1 => $"{Visible[0].Title} - {Visible[0].PercentText}",
        _ => $"{Visible.Count} פעולות מתבצעות"
    };

    /// <param name="canStop">Whether the work checks for pause and cancel.</param>
    public async Task<T> RunAsync<T>(string title, IReadOnlyList<string> paths, bool canStop, Func<OperationProgress, T> work)
    {
        var operation = new FileOperation(title, paths, canStop);
        var task = Task.Run(() => work(operation.Progress));
        running.Add((operation, task));
        StartTimer();

        // Deliberately not awaited: it ends by itself when the operation is over.
        _ = ShowLaterAsync(operation, task);

        try
        {
            return await task;
        }
        finally
        {
            running.RemoveAll(entry => entry.Operation == operation);
            Visible.Remove(operation);
            NotifyChanged();
            if (running.Count == 0)
                timer?.Stop();
            operation.Progress.Dispose();
        }
    }

    public bool IsUsing(string root) => running.Any(entry => entry.Operation.Paths.Any(path =>
        string.Equals(Path.GetPathRoot(path), root, StringComparison.OrdinalIgnoreCase)));

    public async Task CancelAllAsync()
    {
        foreach (var (operation, _) in running.ToList())
            operation.Cancel();

        while (running.Count > 0)
            await Task.Delay(50);
    }

    private async Task ShowLaterAsync(FileOperation operation, Task task)
    {
        await Task.Delay(showDelay);
        if (task.IsCompleted || running.All(entry => entry.Operation != operation))
            return;

        Visible.Add(operation);
        NotifyChanged();
        ShowRequested?.Invoke();
    }

    private void StartTimer()
    {
        if (timer == null)
        {
            timer = new DispatcherTimer { Interval = RefreshInterval };
            timer.Tick += (_, _) =>
            {
                foreach (var (operation, _) in running.ToList())
                    operation.Refresh();
                OnPropertyChanged(nameof(StatusText));
            };
        }
        timer.Start();
    }

    private void NotifyChanged()
    {
        OnPropertyChanged(nameof(HasVisible));
        OnPropertyChanged(nameof(StatusText));
    }
}
