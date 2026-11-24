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

public class FileTransferResult
{
    public List<string> Errors { get; } = new();
    public bool Cancelled { get; set; }
}

public interface IFileTransferService
{
    FileTransferResult Transfer(IEnumerable<string> sources, string targetDirectory, bool move, ConflictResolver resolveConflict);
}

/// <summary>
/// Copies and moves files and folders. Every item is checked against the path policy,
/// and an existing file is never replaced without an explicit decision.
/// </summary>
public class FileTransferService : IFileTransferService
{
    private readonly IPathPolicy policy;
    private readonly IAuditService audit;

    public FileTransferService(IPathPolicy policy, IAuditService audit)
    {
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
        this.audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public FileTransferResult Transfer(IEnumerable<string> sources, string targetDirectory, bool move, ConflictResolver resolveConflict)
    {
        var context = new TransferContext(move, resolveConflict);

        foreach (var source in sources)
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

        return context.Result;
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
        string name = Path.GetFileName(source);

        bool permitted = context.Move
            ? policy.CanMoveFile(source, targetDirectory, out var reason)
            : policy.CanCopyFile(source, targetDirectory, out reason);
        if (!permitted)
        {
            Fail(context, source, name, reason);
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
            if (context.Move)
                File.Move(source, destination, overwrite: true);
            else
                CopyViaTemporaryFile(source, destination);

            audit.Log(context.Move ? AuditEventType.FileMoved : AuditEventType.FileCopied, source, destination, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Fail(context, source, name, ex.Message);
        }
    }

    /// <summary>
    /// Keeps the existing destination intact if the copy fails halfway, for example on a full disk.
    /// </summary>
    private static void CopyViaTemporaryFile(string source, string destination)
    {
        string temporary = Path.Combine(Path.GetDirectoryName(destination)!, $"~zerach-{Guid.NewGuid():N}.tmp");
        try
        {
            File.Copy(source, temporary);
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

        public TransferContext(bool move, ConflictResolver resolver)
        {
            Move = move;
            this.resolver = resolver;
        }

        public bool Move { get; }

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
