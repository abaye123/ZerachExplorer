using LiteExplorer.Infrastructure.Commands;
using LiteExplorer.Infrastructure.Converters;
using LiteExplorer.MVVM.ViewModels.Base;
using LiteExplorer.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LiteExplorer.MVVM.ViewModels;

/// <summary>One running operation as shown in the progress window.</summary>
internal sealed class FileOperation : ViewModel
{
    public const double GraphWidth = 440;
    public const double GraphHeight = 64;

    private static readonly TimeSpan SpeedWindow = TimeSpan.FromSeconds(4);

    private readonly Stopwatch active = Stopwatch.StartNew();
    private readonly Queue<(TimeSpan Time, long Bytes)> recent = new();
    private readonly List<(double Fraction, double Speed)> history = new();
    private bool isCancelling;

    public FileOperation(string title, IReadOnlyList<string> paths, bool canPause)
    {
        Title = title;
        Paths = paths;
        CanPause = canPause;
        PauseCmd = new ActionCommand(_ => TogglePause(), _ => CanPause && !isCancelling);
        CancelCmd = new ActionCommand(_ => Cancel(), _ => CanPause && !isCancelling);
    }

    public OperationProgress Progress { get; } = new();

    public string Title { get; }

    /// <summary>The places this operation uses, so that their drive is not removed meanwhile.</summary>
    public IReadOnlyList<string> Paths { get; }

    public bool CanPause { get; }

    public ICommand PauseCmd { get; }
    public ICommand CancelCmd { get; }

    public bool IsPaused => Progress.IsPaused;

    /// <summary>Without a known amount of work only activity can be shown.</summary>
    public bool IsIndeterminate => Progress.TotalItems == 0;

    public double Fraction => Progress.TotalBytes > 0 ? (double)Progress.DoneBytes / Progress.TotalBytes
        : Progress.TotalItems > 0 ? (double)Progress.DoneItems / Progress.TotalItems : 0;

    public double Percent => Math.Min(100, Fraction * 100);

    public string PercentText => isCancelling ? "מבטל..."
        : IsPaused ? $"מושהה - {Ltr($"{Percent:0}%")} הושלמו"
        : IsIndeterminate ? "בטיפול, נא להמתין..."
        : $"{Ltr($"{Percent:0}%")} הושלמו";

    public bool HasBytes => Progress.TotalBytes > 0;

    public bool HasItems => Progress.TotalItems > 0;

    public string CurrentName => Progress.CurrentName;

    public double BytesPerSecond { get; private set; }

    public string SpeedText => HasBytes && !IsPaused && BytesPerSecond > 0 ? Ltr($"{BytesToNormalSize.FormatBytes(BytesPerSecond)}/s") : string.Empty;

    public string RemainingItemsText => HasBytes
        ? $"{Progress.TotalItems - Progress.DoneItems} ({Ltr(BytesToNormalSize.FormatBytes(Progress.TotalBytes - Progress.DoneBytes))})"
        : $"{Progress.TotalItems - Progress.DoneItems}";

    public string RemainingTimeText
    {
        get
        {
            if (!HasBytes || IsPaused) return "-";
            if (BytesPerSecond <= 0 || active.Elapsed < TimeSpan.FromSeconds(2)) return "מחשב...";

            return Describe(TimeSpan.FromSeconds((Progress.TotalBytes - Progress.DoneBytes) / BytesPerSecond));
        }
    }

    /// <summary>Speed over progress, drawn like the graph of the Windows explorer.</summary>
    public PointCollection Graph
    {
        get
        {
            var points = new PointCollection { new Point(0, GraphHeight) };
            double peak = history.Count == 0 ? 1 : Math.Max(1, history.Max(sample => sample.Speed) * 1.25);
            foreach (var (fraction, speed) in history)
                points.Add(new Point(fraction * GraphWidth, GraphHeight - speed / peak * GraphHeight));
            points.Add(new Point((history.Count == 0 ? 0 : history[^1].Fraction) * GraphWidth, GraphHeight));
            points.Freeze();
            return points;
        }
    }

    public void Cancel()
    {
        isCancelling = true;
        Progress.Cancel();
        Refresh();
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>Takes a sample and updates everything shown.</summary>
    public void Refresh()
    {
        if (!IsPaused)
            Sample();
        OnPropertyChanged(string.Empty);
    }

    private void TogglePause()
    {
        if (IsPaused)
        {
            // The pause itself must not count as slow copying.
            recent.Clear();
            Progress.Resume();
            active.Start();
        }
        else
        {
            Progress.Pause();
            active.Stop();
        }
        Refresh();
    }

    private void Sample()
    {
        var now = active.Elapsed;
        recent.Enqueue((now, Progress.DoneBytes));
        while (recent.Count > 2 && now - recent.Peek().Time > SpeedWindow)
            recent.Dequeue();

        var first = recent.Peek();
        double seconds = (now - first.Time).TotalSeconds;
        if (seconds < 0.2) return;

        BytesPerSecond = Math.Max(0, Progress.DoneBytes - first.Bytes) / seconds;
        if (HasBytes && (history.Count == 0 || Fraction - history[^1].Fraction >= 0.004))
            history.Add((Fraction, BytesPerSecond));
    }

    internal static string Describe(TimeSpan time)
    {
        if (time.TotalSeconds < 5) return "כמה שניות";
        if (time.TotalSeconds < 60) return $"כ-{Math.Ceiling(time.TotalSeconds / 5) * 5:0} שניות";

        int minutes = (int)Math.Ceiling(time.TotalMinutes);
        if (minutes == 1) return "כדקה";
        if (minutes < 60) return $"כ-{minutes} דקות";

        int hours = minutes / 60;
        string text = hours == 1 ? "כשעה" : $"כ-{hours} שעות";
        return minutes % 60 == 0 ? text : $"{text} ו-{minutes % 60} דקות";
    }

    private static string Ltr(string text) => $"\u200E{text}\u200E";
}
