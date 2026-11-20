using LiteExplorer.MVVM.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LiteExplorer.Services;

public class AuditService : IAuditService
{
    private static readonly object FileLock = new();
    private const long MaxLogSizeBytes = 2 * 1024 * 1024; // 2MB rotation threshold
    private readonly string logFilePath;
    private readonly List<AuditEntry> memoryBuffer = new();
    private const int MaxMemoryBufferSize = 200;

    public static AuditService Instance { get; } = new();

    public string LogFilePath => logFilePath;

    public AuditService(string customPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            logFilePath = customPath;
        }
        else
        {
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string baseDir = string.IsNullOrWhiteSpace(programData)
                ? AppDomain.CurrentDomain.BaseDirectory
                : Path.Combine(programData, "ZerachExplorer");

            try
            {
                if (!Directory.Exists(baseDir))
                {
                    Directory.CreateDirectory(baseDir);
                }
                logFilePath = Path.Combine(baseDir, "audit.log");
            }
            catch
            {
                logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audit.log");
            }
        }
    }

    public void Log(AuditEventType eventType, string target, string details = null, bool success = true)
    {
        var entry = new AuditEntry
        {
            Timestamp = DateTime.UtcNow,
            EventType = eventType,
            Target = target ?? string.Empty,
            Details = details ?? string.Empty,
            Success = success
        };

        lock (FileLock)
        {
            memoryBuffer.Add(entry);
            if (memoryBuffer.Count > MaxMemoryBufferSize)
            {
                memoryBuffer.RemoveAt(0);
            }

            try
            {
                RotateLogIfNeeded();
                string line = entry.ToString() + Environment.NewLine;
                File.AppendAllText(logFilePath, line);
            }
            catch
            {
                // Suppress file I/O exceptions in audit logger to prevent crashing host process
            }
        }
    }

    public IReadOnlyList<AuditEntry> GetRecentEntries(int count = 100)
    {
        lock (FileLock)
        {
            if (memoryBuffer.Count > 0)
            {
                return memoryBuffer.TakeLast(count).Reverse().ToList();
            }

            if (!File.Exists(logFilePath))
            {
                return Array.Empty<AuditEntry>();
            }

            try
            {
                var lines = File.ReadLines(logFilePath).TakeLast(count).Reverse();
                return lines.Select(AuditEntry.Parse).Where(e => e != null).ToList();
            }
            catch
            {
                return Array.Empty<AuditEntry>();
            }
        }
    }

    public void ClearLogs()
    {
        lock (FileLock)
        {
            memoryBuffer.Clear();
            try
            {
                if (File.Exists(logFilePath))
                {
                    File.Delete(logFilePath);
                }
            }
            catch
            {
                // Best effort clear
            }
        }
    }

    private void RotateLogIfNeeded()
    {
        try
        {
            if (File.Exists(logFilePath))
            {
                var fi = new FileInfo(logFilePath);
                if (fi.Length > MaxLogSizeBytes)
                {
                    string backupPath = logFilePath + ".old";
                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }
                    File.Move(logFilePath, backupPath);
                }
            }
        }
        catch
        {
            // Ignore rotation errors
        }
    }
}
