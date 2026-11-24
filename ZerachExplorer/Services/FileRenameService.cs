using ZerachExplorer.MVVM.Models;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace ZerachExplorer.Services;

public interface IFileRenameService
{
    /// <summary>Renames a file and keeps its extension and folder.</summary>
    bool Rename(string path, string newStem, [NotNullWhen(false)] out string? error);
}

public class FileRenameService : IFileRenameService
{
    private readonly IPathPolicy policy;
    private readonly IAuditService audit;

    public FileRenameService(IPathPolicy policy, IAuditService audit)
    {
        this.policy = policy;
        this.audit = audit;
    }

    public bool Rename(string path, string newStem, [NotNullWhen(false)] out string? error)
    {
        string fullPath = Path.GetFullPath(path);
        string newPath = Path.Combine(Path.GetDirectoryName(fullPath)!, newStem + Path.GetExtension(fullPath));

        error = TryRename(fullPath, newPath, newStem);
        audit.Log(AuditEventType.FileRenamed, fullPath, error ?? newPath, error == null);
        return error == null;
    }

    private string? TryRename(string fullPath, string newPath, string newStem)
    {
        if (newStem.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || newStem.Contains(".."))
            return "שם הקובץ מכיל תווים בלתי חוקיים או ניסיון לניווט נתיב.";

        if (!policy.CanOpenFile(fullPath, out var reason) || !policy.CanRename(fullPath, newPath, out reason))
            return reason;

        if (File.Exists(newPath) || Directory.Exists(newPath))
            return "פריט עם שם זה כבר קיים.";

        try
        {
            File.Move(fullPath, newPath);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CrashLogger.LogException(ex, $"Rename failed for {fullPath}");
            return ex.Message;
        }
    }
}
