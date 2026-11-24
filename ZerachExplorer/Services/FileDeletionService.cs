using ZerachExplorer.Helpers.WinAPI;
using ZerachExplorer.MVVM.Models;
using System;
using System.IO;

namespace ZerachExplorer.Services;

public interface IFileDeletionService
{
    /// <summary>
    /// False when deleting this path is always permanent.
    /// </summary>
    bool SupportsRecycleBin(string path);

    bool MoveToRecycleBin(string path, out string? error);

    bool DeletePermanently(string path, out string? error, OperationProgress? progress = null);
}

public class FileDeletionService : IFileDeletionService
{
    private readonly IPathPolicy policy;
    private readonly IAuditService audit;

    public FileDeletionService(IPathPolicy policy, IAuditService audit)
    {
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
        this.audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public bool SupportsRecycleBin(string path)
    {
        try
        {
            // Flash drives, optical media and network drives have no recycle bin.
            return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!).DriveType == DriveType.Fixed;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            return false;
        }
    }

    public bool MoveToRecycleBin(string path, out string? error)
    {
        // The shell deletes permanently where there is no bin, so never reach it in that case.
        if (!SupportsRecycleBin(path))
        {
            error = "אין סל מיחזור בכונן זה.";
            return false;
        }

        return Delete(path, "Recycled", out error, fullPath =>
        {
            var (code, aborted) = RecycleOnShellThread(fullPath);
            if (aborted)
                return "המחיקה בוטלה.";
            return code == 0 ? null : $"שגיאת מערכת 0x{code:X}";
        });
    }

    public bool DeletePermanently(string path, out string? error, OperationProgress? progress = null) =>
        Delete(path, "Deleted permanently", out error, fullPath =>
        {
            var failures = new List<string>();
            try
            {
                DeleteTree(fullPath, progress, failures);
            }
            catch (OperationCanceledException)
            {
                return "המחיקה בוטלה. חלק מהפריטים כבר נמחקו.";
            }

            return failures.Count switch
            {
                0 => null,
                1 => failures[0],
                _ => $"{failures[0]} (ועוד {failures.Count - 1} פריטים)"
            };
        });

    private static void DeleteTree(string path, OperationProgress? progress, List<string> failures)
    {
        progress?.Checkpoint();
        progress?.Begin(Path.GetFileName(path));
        try
        {
            var attributes = File.GetAttributes(path);
            bool isDirectory = (attributes & FileAttributes.Directory) != 0;
            bool isLink = (attributes & FileAttributes.ReparsePoint) != 0;

            // A link is removed itself; what it points to is not touched.
            if (isDirectory && !isLink)
            {
                foreach (var child in Directory.GetFileSystemEntries(path))
                    DeleteTree(child, progress, failures);
            }

            if ((attributes & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);

            if (isDirectory)
                Directory.Delete(path, recursive: false);
            else
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            failures.Add($"{Path.GetFileName(path)}: {ex.Message}");
        }
        progress?.Finish(0);
    }

    /// <summary>The shell needs a single-threaded apartment, and must not block the window.</summary>
    private static (int Code, bool Aborted) RecycleOnShellThread(string path)
    {
        int code = 0;
        bool aborted = false;
        var thread = new Thread(() => code = Interop.SendToRecycleBin(path, out aborted));
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return (code, aborted);
    }

    private bool Delete(string path, string details, out string? error, Func<string, string?> action)
    {
        if (policy.CanDelete(path, out error))
        {
            try
            {
                error = action(Path.GetFullPath(path));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = ex.Message;
            }
        }

        bool success = error == null;
        audit.Log(AuditEventType.FileDeleted, path, error ?? details, success);
        return success;
    }
}
