using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace ZerachExplorer.Services;

/// <summary>
/// Daily log files that are deleted by age and by total size, so daily use cannot fill the disk.
/// </summary>
public sealed class RollingLog
{
    private const long MinimumPartBytes = 64 * 1024;
    private const int PartsPerLimit = 5;

    private readonly object gate = new();
    private readonly string directory;
    private readonly string prefix;
    private readonly Func<DateTime> now;
    private string? currentPath;
    private DateTime currentDay;
    private int retentionDays;
    private long maxTotalBytes;

    public RollingLog(string directory, string prefix, int retentionDays, long maxTotalBytes, Func<DateTime>? clock = null)
    {
        this.directory = directory;
        this.prefix = prefix;
        this.retentionDays = retentionDays;
        this.maxTotalBytes = maxTotalBytes;
        now = clock ?? (() => DateTime.Now);
    }

    public string CurrentPath
    {
        get
        {
            lock (gate)
            {
                return currentPath ?? PartPath(now().Date, LastPart(now().Date));
            }
        }
    }

    private long PartBytes => Math.Max(MinimumPartBytes, maxTotalBytes / PartsPerLimit);

    public void SetLimits(int days, long totalBytes)
    {
        lock (gate)
        {
            retentionDays = days;
            maxTotalBytes = totalBytes;
            Prune();
        }
    }

    public void Append(string text)
    {
        lock (gate)
        {
            Directory.CreateDirectory(directory);

            var today = now().Date;
            if (currentPath == null || today != currentDay)
            {
                currentDay = today;
                currentPath = PartPath(today, LastPart(today));
                Prune();
            }

            var info = new FileInfo(currentPath);
            if (info.Exists && info.Length >= PartBytes)
            {
                currentPath = PartPath(today, LastPart(today) + 1);
                Prune();
            }

            File.AppendAllText(currentPath, text, Encoding.UTF8);
        }
    }

    /// <summary>Lines from all retained files, oldest first.</summary>
    public IReadOnlyList<string> ReadLines()
    {
        lock (gate)
        {
            return Parts().SelectMany(part => File.ReadLines(part.Path)).ToList();
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            foreach (var part in Parts())
                File.Delete(part.Path);
            currentPath = null;
        }
    }

    private void Prune()
    {
        var parts = Parts();
        var oldest = now().Date.AddDays(1 - retentionDays);
        // Room is reserved for the part being written, so the total stays within the limit.
        long available = maxTotalBytes - PartBytes;

        for (int i = parts.Count - 1; i >= 0; i--)
        {
            var part = parts[i];
            if (string.Equals(part.Path, currentPath, StringComparison.OrdinalIgnoreCase))
                continue;

            available -= new FileInfo(part.Path).Length;
            if (part.Day >= oldest && available >= 0)
                continue;

            try
            {
                File.Delete(part.Path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // In use elsewhere; the next prune retries.
                Debug.WriteLine(ex.Message);
            }
        }
    }

    private string PartPath(DateTime day, int part) =>
        Path.Combine(directory, $"{prefix}-{day.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-{part:D3}.log");

    private int LastPart(DateTime day) => Parts().Where(p => p.Day == day).Select(p => p.Number).DefaultIfEmpty(1).Max();

    private List<(string Path, DateTime Day, int Number)> Parts()
    {
        var parts = new List<(string Path, DateTime Day, int Number)>();
        if (!Directory.Exists(directory))
            return parts;

        foreach (var path in Directory.GetFiles(directory, prefix + "-*.log"))
        {
            var fields = Path.GetFileNameWithoutExtension(path)[(prefix.Length + 1)..].Split('-');
            if (fields.Length == 2 &&
                DateTime.TryParseExact(fields[0], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) &&
                int.TryParse(fields[1], out int number))
            {
                parts.Add((path, day, number));
            }
        }

        return parts.OrderBy(p => p.Day).ThenBy(p => p.Number).ToList();
    }
}
