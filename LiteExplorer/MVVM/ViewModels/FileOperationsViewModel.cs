using LiteExplorer.Infrastructure.Commands;
using LiteExplorer.MVVM.Models;
using LiteExplorer.Services;
using System.IO;
using System.Windows.Input;

namespace LiteExplorer.MVVM.ViewModels;

/// <summary>What the file commands need to know about the tab they act on.</summary>
internal interface IExplorerTab
{
    string? TabPath { get; }
    bool IsLoading { set; }
    FileSystemObject? CurrentFileSystemObject { get; }
    IReadOnlyList<FileSystemObject> SelectedFileSystemObjects { get; }
    void Reload();
    void GoHome();
}

internal sealed class FileOperationsViewModel
{
    private readonly IExplorerTab tab;
    private readonly ExplorerServices services;
    private readonly IPathPolicy policy;
    private readonly IDialogService dialogs;

    public FileOperationsViewModel(IExplorerTab tab, ExplorerServices services)
    {
        this.tab = tab;
        this.services = services;
        policy = services.Policy;
        dialogs = services.Dialogs;

        CopyCmd = new ActionCommand(p => PlaceOnClipboard(p, isCut: false), p => policy.Permissions.CanCopyFrom && HasTarget(p));
        CutCmd = new ActionCommand(p => PlaceOnClipboard(p, isCut: true),
            p => policy.Permissions.CanCopyFrom && policy.Permissions.CanDelete && HasTarget(p));
        PasteCmd = new ActionCommand(async _ => await PasteAsync(),
            _ => policy.Permissions.CanCopyTo && IsInFolder && services.Clipboard.HasFiles());
        DeleteCmd = new ActionCommand(async p => await DeleteAsync(p), p => policy.Permissions.CanDelete && HasTarget(p));
        RenameCmd = new ActionCommand(Rename, p => policy.Permissions.CanRename && GetTargets(p).Count == 1);
        CreateDirectoryCmd = new ActionCommand(_ => CreateDirectory(), _ => policy.Permissions.CanCreateDirectory && IsInFolder);
        EjectDriveCmd = new ActionCommand(async p => await EjectAsync(p), p => FindRemovableDrive(p) != null);
        CalculateFolderSizeCmd = new ActionCommand(async p => await CalculateFolderSizeAsync(p));
    }

    public ICommand CopyCmd { get; }
    public ICommand CutCmd { get; }
    public ICommand PasteCmd { get; }
    public ICommand DeleteCmd { get; }
    public ICommand RenameCmd { get; }
    public ICommand CreateDirectoryCmd { get; }
    public ICommand EjectDriveCmd { get; }
    public ICommand CalculateFolderSizeCmd { get; }

    private bool IsInFolder => !string.IsNullOrEmpty(tab.TabPath);

    private bool HasTarget(object? parameter) => GetTargets(parameter).Count > 0;

    private List<string> GetTargets(object? parameter)
    {
        if (parameter is string path && !string.IsNullOrWhiteSpace(path))
            return new List<string> { path.Trim() };

        var items = tab.SelectedFileSystemObjects;
        if (items.Count == 0 && tab.CurrentFileSystemObject is { } current)
            items = new[] { current };

        return items.Select(item => item.Path)
            .Where(itemPath => !string.IsNullOrEmpty(itemPath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void PlaceOnClipboard(object? parameter, bool isCut)
    {
        var permissions = policy.Permissions;
        if (!permissions.CanCopyFrom || (isCut && !permissions.CanDelete))
            return;

        var paths = GetTargets(parameter)
            .Where(path => Directory.Exists(path) ? policy.CanNavigate(path, out _) : policy.CanOpenFile(path, out _))
            .ToList();
        if (paths.Count == 0)
            return;

        if (!services.Clipboard.Write(paths, isCut))
            dialogs.Warn("לוח ההעתקה תפוס על ידי תוכנה אחרת. נסה שוב.", "העתקה");

        CommandManager.InvalidateRequerySuggested();
    }

    private async Task PasteAsync()
    {
        if (!TryGetTargetFolder("הדבקה", "לא ניתן להדביק פריטים במסך הראשי. אנא בחר כונן או תיקייה תחילה.", out var target))
            return;

        var sources = services.Clipboard.Read(out bool isCut);
        if (sources.Count == 0)
            return;

        tab.IsLoading = true;
        try
        {
            var result = await Task.Run(() => services.Transfer.Transfer(sources, target, isCut, dialogs.AskConflict));

            if (isCut && !result.Cancelled)
                services.Clipboard.Clear();

            tab.Reload();

            if (result.Errors.Count > 0)
                dialogs.Warn($"חלק מהקבצים לא הועברו:\n\n{Summarize(result.Errors)}", "הודעת העברה");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CrashLogger.LogException(ex, $"Paste into {target} failed");
            dialogs.Error($"ההעברה נכשלה: {ex.Message}", "שגיאה");
        }
        finally
        {
            tab.IsLoading = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private async Task DeleteAsync(object? parameter)
    {
        var allowed = new List<string>();
        var errors = new List<string>();

        foreach (var target in GetTargets(parameter))
        {
            if (!policy.CanDelete(target, out var reason))
            {
                services.Audit.Log(AuditEventType.FileDeleted, target, reason, false);
                errors.Add($"{Path.GetFileName(target)}: {reason}");
            }
            else if (!Path.Exists(target))
            {
                errors.Add($"{Path.GetFileName(target)}: הפריט אינו קיים.");
            }
            else
            {
                allowed.Add(target);
            }
        }

        if (allowed.Count > 0)
        {
            bool recyclable = allowed.All(services.Deletion.SupportsRecycleBin);
            if (!await ConfirmDeletionAsync(allowed, recyclable))
                return;

            foreach (var target in allowed)
            {
                string? error = null;
                // The shell shows its own windows, so recycling stays on the UI thread.
                bool deleted = recyclable
                    ? services.Deletion.MoveToRecycleBin(target, out error)
                    : await Task.Run(() => services.Deletion.DeletePermanently(target, out error));
                if (!deleted)
                    errors.Add($"{Path.GetFileName(target)}: {error}");
            }

            tab.Reload();
            CommandManager.InvalidateRequerySuggested();
        }

        if (errors.Count > 0)
            dialogs.Warn($"שגיאה במחיקת חלק מהפריטים:\n{Summarize(errors)}", "שגיאה במחיקה");
    }

    private async Task<bool> ConfirmDeletionAsync(List<string> targets, bool recyclable)
    {
        string subject = targets.Count == 1 ? $"את הפריט:\n{targets[0]}" : $"{targets.Count} פריטים";
        string details = await FolderInspector.DescribeContentsAsync(targets.Where(Directory.Exists).ToList());
        string consequence = recyclable
            ? "הפריטים יועברו לסל המיחזור."
            : "שים לב: אין בכונן זה סל מיחזור. המחיקה סופית ולא ניתן יהיה לשחזר!";

        return dialogs.Confirm(
            $"האם אתה בטוח שברצונך למחוק {subject}?\n\n{details}{consequence}",
            recyclable ? "אישור מחיקה" : "אזהרת מחיקה לצמיתות");
    }

    private void Rename(object? parameter)
    {
        var targets = GetTargets(parameter);
        if (targets.Count != 1 || !File.Exists(targets[0]))
        {
            dialogs.Error("ניתן לשנות שם לקבצים בלבד.", "שגיאה");
            return;
        }

        string path = targets[0];
        if (!policy.CanOpenFile(path, out var reason))
        {
            services.Audit.Log(AuditEventType.FileRenamed, path, reason, false);
            dialogs.Warn($"לא ניתן לשנות שם לקובץ זה:\n{reason}", "גישה נדחתה");
            return;
        }

        string currentName = Path.GetFileNameWithoutExtension(path);
        string? newName = dialogs.AskText("הכנס את השם החדש לקובץ:", "שינוי שם הקובץ", currentName);
        if (newName == null || string.Equals(newName, currentName, StringComparison.OrdinalIgnoreCase))
            return;

        if (services.Rename.Rename(path, newName, out var error))
            tab.Reload();
        else
            dialogs.Error($"שינוי השם נכשל: {error}", "שגיאה");
    }

    private void CreateDirectory()
    {
        if (!TryGetTargetFolder("יצירת תיקייה", "לא ניתן ליצור תיקייה במסך הראשי. אנא בחר כונן או תיקייה תחילה.", out var parent) ||
            !dialogs.AskNewFolder(parent, out var name))
            return;

        string path = Path.Combine(parent, name);
        try
        {
            Directory.CreateDirectory(path);
            services.Audit.Log(AuditEventType.DirectoryCreated, path, "Created directory");
            tab.Reload();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CrashLogger.LogException(ex, $"Failed to create folder {path}");
            services.Audit.Log(AuditEventType.DirectoryCreated, path, ex.Message, false);
            dialogs.Error($"יצירת התיקייה נכשלה: {ex.Message}", "שגיאה");
        }
    }

    private async Task EjectAsync(object? parameter)
    {
        string? drive = FindRemovableDrive(parameter);
        if (drive == null)
        {
            dialogs.Info("נא לבחור כונן נשלף להוצאה.", "הוצאת התקן");
            return;
        }

        // Folder watchers in every open tab hold the volume open and would fail the lock.
        services.Devices.NotifyDriveReleasing(drive);
        bool success;
        string message = string.Empty;
        try
        {
            success = await Task.Run(() => services.Eject.EjectDrive(drive, out message));
        }
        finally
        {
            services.Devices.NotifyDriveReleaseFinished(drive);
        }

        services.Audit.Log(AuditEventType.DriveEjected, drive, message, success);
        if (success)
        {
            dialogs.Info(message, "הוצאת התקן USB");
            tab.GoHome();
        }
        else
        {
            dialogs.Warn(message, "הוצאת התקן USB");
        }
    }

    private string? FindRemovableDrive(object? parameter)
    {
        if (parameter is string path && !string.IsNullOrEmpty(path))
            return DriveDescriber.IsRemovable(path) ? Path.GetPathRoot(path) : null;

        var candidate = tab.CurrentFileSystemObject is { IsRemovableDrive: true } current
            ? current
            : tab.SelectedFileSystemObjects.FirstOrDefault(item => item.IsRemovableDrive);
        return candidate == null ? null : Path.GetPathRoot(candidate.Path);
    }

    private async Task CalculateFolderSizeAsync(object? parameter)
    {
        var item = parameter as FileSystemObject ?? tab.CurrentFileSystemObject;
        if (item is not { IsDirectory: true, IsCalculatingSize: false } || string.IsNullOrWhiteSpace(item.Path))
            return;

        item.IsCalculatingSize = true;
        try
        {
            item.Size = await FolderInspector.CalculateSizeAsync(item.Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CrashLogger.LogMessage($"Failed to calculate folder size for {item.Path}: {ex.Message}", "WARN");
            dialogs.Warn($"חישוב גודל התיקייה נכשל: {ex.Message}", "גודל תיקייה");
        }
        finally
        {
            item.IsCalculatingSize = false;
        }
    }

    private bool TryGetTargetFolder(string title, string homeMessage, out string folder)
    {
        folder = tab.TabPath ?? string.Empty;
        if (!Directory.Exists(folder))
        {
            dialogs.Info(homeMessage, title);
            return false;
        }

        if (policy.CanNavigate(folder, out var reason))
            return true;

        services.Audit.Log(AuditEventType.SecurityBlocked, folder, reason, false);
        dialogs.Warn(reason, "גישה נדחתה");
        return false;
    }

    private static string Summarize(List<string> errors)
    {
        string text = string.Join("\n", errors.Take(5));
        return errors.Count > 5 ? text + $"\nועוד {errors.Count - 5} שגיאות נוספות." : text;
    }
}
