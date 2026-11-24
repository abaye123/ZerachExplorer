using LiteExplorer.MVVM.Models;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace LiteExplorer.Services;

public interface IFolderCreationService
{
    bool Create(string parentDirectory, string name, [NotNullWhen(false)] out string? error);
}

public class FolderCreationService : IFolderCreationService
{
    private readonly IPathPolicy policy;
    private readonly IAuditService audit;

    public FolderCreationService(IPathPolicy policy, IAuditService audit)
    {
        this.policy = policy;
        this.audit = audit;
    }

    public bool Create(string parentDirectory, string name, [NotNullWhen(false)] out string? error)
    {
        string path = Path.Combine(parentDirectory, name);
        error = TryCreate(parentDirectory, name, path);
        audit.Log(AuditEventType.DirectoryCreated, path, error ?? "Created directory", error == null);
        return error == null;
    }

    private string? TryCreate(string parentDirectory, string name, string path)
    {
        if (!policy.CanCreateDirectory(parentDirectory, name, out var reason))
            return reason;
        if (Path.Exists(path))
            return "פריט עם שם זה כבר קיים.";

        try
        {
            Directory.CreateDirectory(path);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            CrashLogger.LogException(ex, $"Failed to create folder {path}");
            return ex.Message;
        }
    }
}
