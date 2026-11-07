using System;
using System.IO;
using System.Text;

namespace LiteExplorer.Services;

public static class CrashLogger
{
    private static readonly object LogLock = new();

    public static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ZerachExplorer",
        "logs"
    );

    public static string LogFilePath => Path.Combine(LogDirectory, "crash.log");

    public static void LogException(Exception ex, string context = null)
    {
        if (ex == null) return;

        try
        {
            lock (LogLock)
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }

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

                File.AppendAllText(LogFilePath, sb.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Fail silently to prevent crashing inside the crash handler
        }
    }

    public static void LogMessage(string message, string level = "INFO")
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        try
        {
            lock (LogLock)
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }

                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(LogFilePath, entry, Encoding.UTF8);
            }
        }
        catch
        {
            // Fail silently
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
            string line;
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
