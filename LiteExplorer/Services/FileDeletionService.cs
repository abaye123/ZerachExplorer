using LiteExplorer.Helpers.WinAPI;
using LiteExplorer.MVVM.Models;
using System;
using System.IO;

namespace LiteExplorer.Services;

public interface IFileDeletionService
{
    /// <summary>
    /// False when deleting this path is always permanent.
    /// </summary>
    bool SupportsRecycleBin(string path);

    bool MoveToRecycleBin(string path, out string? error);

    bool DeletePermanently(string path, out string? error);
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
            int code = Interop.SendToRecycleBin(fullPath, out bool aborted);
            if (aborted)
                return "המחיקה בוטלה.";
            return code == 0 ? null : $"שגיאת מערכת 0x{code:X}";
        });
    }

    public bool DeletePermanently(string path, out string? error) => Delete(path, "Deleted permanently", out error, fullPath =>
    {
        if (Directory.Exists(fullPath))
            Directory.Delete(fullPath, recursive: true);
        else
            File.Delete(fullPath);
        return null;
    });

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
