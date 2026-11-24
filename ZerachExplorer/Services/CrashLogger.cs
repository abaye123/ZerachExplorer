using ZerachExplorer.MVVM.Models;
using System;
using System.IO;
using System.Text;

namespace ZerachExplorer.Services;

public static class CrashLogger
{
    private static readonly object LogLock = new();

    private const long MaxTotalBytes = 2 * 1024 * 1024;

    private static RollingLog log = CreateLog(DefaultDirectory);

    private static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ZerachExplorer",
        "logs");

    public static string LogDirectory { get; private set; } = DefaultDirectory;

    public static string LogFilePath => log.CurrentPath;

    public static void SetRetention(int days)
    {
        try
        {
            log.SetLimits(days, MaxTotalBytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogException(ex, "Applying crash log limits");
        }
    }

    /// <summary>Keeps automated tests away from the real user logs.</summary>
    internal static void Redirect(string directory)
    {
        LogDirectory = directory;
        log = CreateLog(directory);
    }

    private static RollingLog CreateLog(string directory) =>
        new(directory, "crash", AppConfiguration.DefaultLogRetentionDays, MaxTotalBytes);

    public static void LogException(Exception? ex, string? context = null)
    {
        if (ex == null) return;

        try
        {
            lock (LogLock)
            {
                var sb = new StringBuilder();
                sb.AppendLine("================================================================================");
                sb.AppendLine($"[CRASH REPORT] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} (UTC: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff})");
                if (!string.IsNullOrWhiteSpace(context))
                {
                    sb.AppendLine($"Context: {context}");
                }
                sb.AppendLine($"OS: {Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")})");
                sb.AppendLine($".NET Runtime: {Environment.Version}");
                sb.AppendLine($"Machine: {Environment.MachineName} | User: {Environment.UserName}");
                sb.AppendLine("--------------------------------------------------------------------------------");

                AppendExceptionDetails(sb, ex, 0);

                sb.AppendLine("================================================================================");
                sb.AppendLine();

                log.Append(sb.ToString());
            }
        }
        catch (Exception secondary)
        {
            // Nowhere left to report: the log itself failed.
            System.Diagnostics.Debug.WriteLine(secondary);
        }
    }

    public static void LogMessage(string message, string level = "INFO")
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        try
        {
            lock (LogLock)
            {
                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";
                log.Append(entry);
            }
        }
        catch (Exception secondary)
        {
            System.Diagnostics.Debug.WriteLine(secondary);
        }
    }

    private static void AppendExceptionDetails(StringBuilder sb, Exception ex, int depth)
    {
        string indent = new string(' ', depth * 2);
        sb.AppendLine($"{indent}Type: {ex.GetType().FullName}");
        sb.AppendLine($"{indent}Message: {ex.Message}");
        sb.AppendLine($"{indent}HResult: 0x{ex.HResult:X8}");

        if (!string.IsNullOrEmpty(ex.StackTrace))
        {
            sb.AppendLine($"{indent}StackTrace:");
            using var reader = new StringReader(ex.StackTrace);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                sb.AppendLine($"{indent}  {line}");
            }
        }

        if (ex.InnerException != null)
        {
            sb.AppendLine($"{indent}Inner Exception:");
            AppendExceptionDetails(sb, ex.InnerException, depth + 1);
        }
    }
}
