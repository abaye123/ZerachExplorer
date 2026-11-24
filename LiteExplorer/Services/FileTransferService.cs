using LiteExplorer.Helpers.WinAPI;
using LiteExplorer.MVVM.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace LiteExplorer.Services;

/// <summary>
/// Asks how to handle a destination file that already exists.
/// Arguments: file name, existing path, incoming path.
/// </summary>
public delegate ConflictResolutionResult ConflictResolver(string fileName, string existingPath, string incomingPath);

/// <summary>Asks whether to start although the destination seems too small.</summary>
public delegate bool LowSpacePrompt(long neededBytes, long freeBytes);

public class FileTransferResult
{
    public List<string> Errors { get; } = new();
    public bool Cancelled { get; set; }
}

public interface IFileTransferService
{
    FileTransferResult Transfer(IEnumerable<string> sources, string targetDirectory, bool move, ConflictResolver resolveConflict,
        OperationProgress? progress = null, LowSpacePrompt? confirmLowSpace = null);
}

/// <summary>
/// Copies and moves files and folders. Every item is checked against the path policy,
/// and an existing file is never replaced without an explicit decision.
/// </summary>
public class FileTransferService : IFileTransferService
{
    private readonly IPathPolicy policy;
    private const string TemporaryPrefix = "~zerach-";
    private const string TemporarySuffix = ".tmp";

    private readonly IAuditService audit;
    private readonly Func<string, string, bool> isSameVolume;
    private readonly Func<string, long?> freeSpace;

    public FileTransferService(IPathPolicy policy, IAuditService audit)
        : this(policy, audit, HasSameRoot, GetFreeSpace)
    {
    }

    internal FileTransferService(IPathPolicy policy, IAuditService audit, Func<string, string, bool> isSameVolume, Func<string, long?> freeSpace)
    {
        this.isSameVolume = isSameVolume;
        this.freeSpace = freeSpace;
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
        this.audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    /// <summary>Files in progress, which are not shown to the user.</summary>
    public static bool IsTemporaryName(string name) =>
        name.StartsWith(TemporaryPrefix, StringComparison.OrdinalIgnoreCase) && name.EndsWith(TemporarySuffix, StringComparison.OrdinalIgnoreCase);

    public FileTransferResult Transfer(IEnumerable<string> sources, string targetDirectory, bool move, ConflictResolver resolveConflict,
        OperationProgress? progress = null, LowSpacePrompt? confirmLowSpace = null)
    {
        using var ownProgress = progress == null ? new OperationProgress() : null;
        var context = new TransferContext(move, targetDirectory, resolveConflict, progress ?? ownProgress!);
        var items = sources.ToList();

        try
        {
            long copied = Measure(items, context);
            if (freeSpace(targetDirectory) is { } free && copied > free && confirmLowSpace?.Invoke(copied, free) == false)
            {
                context.Result.Cancelled = true;
                return context.Result;
            }

            RemoveAbandonedFiles(targetDirectory);

            foreach (var source in items)
            {
                if (context.Result.Cancelled)
                    break;

                if (File.Exists(source))
                    TransferFile(source, targetDirectory, context);
                else if (Directory.Exists(source))
                    TransferRootDirectory(source, targetDirectory, context);
                else
                    context.Result.Errors.Add($"הפריט '{source}' אינו קיים במערכת.");
            }
        }
        catch (OperationCanceledException)
        {
            context.Result.Cancelled = true;
        }

        return context.Result;
    }

    /// <summary>Counts the work ahead and returns the bytes that have to be copied.</summary>
    private long Measure(List<string> sources, TransferContext context)
    {
        long copied = 0;
        foreach (var source in sources)
        {
            bool sameVolume = context.Move && isSameVolume(source, context.TargetDirectory);
            try
            {
                var files = File.Exists(source)
                    ? new[] { new FileInfo(source) }
                    : Directory.Exists(source) ? new DirectoryInfo(source).EnumerateFiles("*", MeasureOptions) : Array.Empty<FileInfo>();

                foreach (var file in files)
                {
                    context.Progress.Checkpoint();
                    context.Progress.AddTotal(1, file.Length);
                    if (!sameVolume)
                        copied += file.Length;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Only the estimate suffers; the transfer itself reports the problem.
                CrashLogger.LogMessage($"Measuring {source}: {ex.Message}", "WARN");
            }
        }
        return copied;
    }

    private static readonly EnumerationOptions MeasureOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    /// <summary>Removes partial files left by a power failure or a disconnected drive.</summary>
    private static void RemoveAbandonedFiles(string directory)
    {
        try
        {
            foreach (var file in new DirectoryInfo(directory).EnumerateFiles(TemporaryPrefix + "*" + TemporarySuffix))
            {
                if (DateTime.UtcNow - file.LastWriteTimeUtc > TimeSpan.FromDays(1) && DateTime.UtcNow - file.CreationTimeUtc > TimeSpan.FromDays(1))
                    file.Delete();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CrashLogger.LogMessage($"Cleaning partial files in {directory}: {ex.Message}", "WARN");
        }
    }

    private static bool HasSameRoot(string first, string second) =>
        string.Equals(Path.GetPathRoot(Path.GetFullPath(first)), Path.GetPathRoot(Path.GetFullPath(second)), StringComparison.OrdinalIgnoreCase);

    private static long? GetFreeSpace(string directory)
    {
        try
        {
            return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(directory))!).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private void TransferRootDirectory(string source, string targetDirectory, TransferContext context)
    {
        string name = Path.GetFileName(source.TrimEnd('\\', '/'));

        if (!policy.CanTransferDirectory(source, targetDirectory, context.Move, out var reason))
        {
            Fail(context, source, name, reason);
            return;
        }

        string destination = Path.Combine(targetDirectory, name);
        if (PathsEqual(source, destination))
        {
            if (context.Move)
                return;

            destination = GetUniquePath(targetDirectory, name, isDirectory: true);
        }

        TransferDirectory(source, destination, context);
    }

    private void TransferDirectory(string source, string destination, TransferContext context)
    {
        string name = Path.GetFileName(source);

        if (!policy.CanNavigate(source, out var reason))
        {
            Fail(context, source, name, reason);
            return;
        }

        // An existing folder may be a link that leads outside the allowed area.
        if (Directory.Exists(destination) && !policy.CanNavigate(destination, out reason))
        {
            Fail(context, source, name, reason);
            return;
        }

        try
        {
            Directory.CreateDirectory(destination);

            foreach (var file in Directory.GetFiles(source))
            {
                if (context.Result.Cancelled) return;
                TransferFile(file, destination, context);
            }

            foreach (var directory in Directory.GetDirectories(source))
            {
                if (context.Result.Cancelled) return;
                TransferDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)), context);
            }

            if (context.Move && Directory.GetFileSystemEntries(source).Length == 0)
                Directory.Delete(source, recursive: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Fail(context, source, name, ex.Message);
        }
    }

    private void TransferFile(string source, string targetDirectory, TransferContext context)
    {
        context.Progress.Checkpoint();
        context.Progress.Begin(Path.GetFileName(source));
        long size = 0;
        try
        {
            size = new FileInfo(source).Length;
            TransferSingleFile(source, targetDirectory, context);
        }
        catch (FileNotFoundException ex)
        {
            Fail(context, source, Path.GetFileName(source), ex.Message);
        }
        finally
        {
            context.Progress.Finish(size);
        }
    }

    private void TransferSingleFile(string source, string targetDirectory, TransferContext context)
    {
        string name = Path.GetFileName(source);

        bool permitted = context.Move
            ? policy.CanMoveFile(source, targetDirectory, out var reason)
            : policy.CanCopyFile(source, targetDirectory, out reason);
        if (!permitted)
        {
            Fail(context, source, name, reason!);
            return;
        }

        string destination = Path.Combine(targetDirectory, name);

        if (PathsEqual(source, destination))
        {
            if (context.Move)
                return;

            destination = GetUniquePath(targetDirectory, name, isDirectory: false);
        }
        else if (Directory.Exists(destination))
        {
            Fail(context, source, name, "קיימת ביעד תיקייה באותו שם.");
            return;
        }
        else if (File.Exists(destination))
        {
            switch (context.Resolve(name, destination, source))
            {
                case ConflictResolutionAction.Cancel:
                    context.Result.Cancelled = true;
                    return;
                case ConflictResolutionAction.Skip:
                    return;
                case ConflictResolutionAction.KeepBoth:
                    destination = GetUniquePath(targetDirectory, name, isDirectory: false);
                    break;
                case ConflictResolutionAction.Replace:
                    // Replacing a link would write through to whatever it points at.
                    if ((File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0)
                    {
                        Fail(context, source, name, "לא ניתן להחליף קישור.");
                        return;
                    }
                    break;
            }
        }

        try
        {
            if (context.Move && isSameVolume(source, destination))
            {
                File.Move(source, destination, overwrite: true);
            }
            else
            {
                CopyViaTemporaryFile(source, destination, context.Progress);
                if (context.Move)
                    DeleteSource(source);
            }

            audit.Log(context.Move ? AuditEventType.FileMoved : AuditEventType.FileCopied, source, destination, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Fail(context, source, name, ex.Message);

            // A disconnected drive would otherwise fail every remaining file.
            if (!Directory.Exists(context.TargetDirectory))
            {
                context.Result.Errors.Add("תיקיית היעד אינה זמינה עוד. ההעברה הופסקה.");
                context.Result.Cancelled = true;
            }
        }
    }

    /// <summary>
    /// Keeps the existing destination intact if the copy fails or is cancelled halfway.
    /// </summary>
    private static void CopyViaTemporaryFile(string source, string destination, OperationProgress progress)
    {
        string temporary = Path.Combine(Path.GetDirectoryName(destination)!, $"{TemporaryPrefix}{Guid.NewGuid():N}{TemporarySuffix}");
        try
        {
            FileCopy.Copy(source, temporary, progress);
            File.Move(temporary, destination, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                CrashLogger.LogMessage($"Could not remove temporary file {temporary}: {ex.Message}", "WARN");
            }
            throw;
        }
    }

    /// <summary>Reached only after the copy is complete.</summary>
    private static void DeleteSource(string source)
    {
        try
        {
            var attributes = File.GetAttributes(source);
            if ((attributes & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(source, attributes & ~FileAttributes.ReadOnly);
            File.Delete(source);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"הקובץ הועתק, אך המקור לא נמחק: {ex.Message}", ex);
        }
    }

    private void Fail(TransferContext context, string source, string name, string reason)
    {
        context.Result.Errors.Add($"{name}: {reason}");
        audit.Log(context.Move ? AuditEventType.FileMoved : AuditEventType.FileCopied, source, reason, false);
    }

    private static bool PathsEqual(string first, string second) => string.Equals(
        Path.GetFullPath(first).TrimEnd('\\', '/'),
        Path.GetFullPath(second).TrimEnd('\\', '/'),
        StringComparison.OrdinalIgnoreCase);

    private static string GetUniquePath(string directory, string name, bool isDirectory)
    {
        string stem = isDirectory ? name : Path.GetFileNameWithoutExtension(name);
        string extension = isDirectory ? string.Empty : Path.GetExtension(name);

        for (int counter = 1; ; counter++)
        {
            string candidate = Path.Combine(directory, $"{stem} ({counter}){extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
                return candidate;
        }
    }

    private sealed class TransferContext
    {
        private readonly ConflictResolver resolver;
        private ConflictResolutionAction? actionForAll;

        public TransferContext(bool move, string targetDirectory, ConflictResolver resolver, OperationProgress progress)
        {
            Move = move;
            TargetDirectory = targetDirectory;
            Progress = progress;
            this.resolver = resolver;
        }

        public bool Move { get; }

        public string TargetDirectory { get; }

        public OperationProgress Progress { get; }

        public FileTransferResult Result { get; } = new();

        public ConflictResolutionAction Resolve(string name, string existing, string incoming)
        {
            if (actionForAll.HasValue)
                return actionForAll.Value;

            var answer = resolver(name, existing, incoming);
            if (answer == null)
                return ConflictResolutionAction.Cancel;

            if (answer.ApplyToAll && answer.Action != ConflictResolutionAction.Cancel)
                actionForAll = answer.Action;

            return answer.Action;
        }
    }
}
