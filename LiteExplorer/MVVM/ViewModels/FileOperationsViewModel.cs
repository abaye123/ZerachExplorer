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
        RenameCmd = new ActionCommand(async p => await RenameAsync(p), p => policy.Permissions.CanRename && GetTargets(p).Count == 1);
        CreateDirectoryCmd = new ActionCommand(async _ => await CreateDirectoryAsync(), _ => policy.Permissions.CanCreateDirectory && IsInFolder);
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
        if (tab.TabPath is not { } target)
            return;

        var sources = services.Clipboard.Read(out bool isCut);
        if (sources.Count == 0)
            return;

        string count = sources.Count == 1 ? "פריט אחד" : $"{sources.Count} פריטים";
        string title = $"{(isCut ? "העברת" : "העתקת")} {count} אל {NameOf(target)}";
        try
        {
            string? denied = null;
            var result = await services.Operations.RunAsync(title, sources.Append(target).ToList(), canStop: true, progress =>
                policy.CanNavigate(target, out denied)
                    ? services.Transfer.Transfer(sources, target, isCut, dialogs.AskConflict, progress, dialogs.ConfirmLowSpace)
                    : null);

            if (result == null)
            {
                services.Audit.Log(AuditEventType.SecurityBlocked, target, denied, false);
                dialogs.Warn(denied!, "גישה נדחתה");
                return;
            }

            if (isCut && !result.Cancelled)
                services.Clipboard.Clear();

            tab.Reload();

            if (result.Errors.Count > 0)
                dialogs.Warn($"חלק מהקבצים לא הועברו:\n\n{Summarize(result.Errors)}", "העברת קבצים");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CrashLogger.LogException(ex, $"Paste into {target} failed");
            dialogs.Error($"ההעברה נכשלה: {ex.Message}", "שגיאה");
        }
        finally
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private async Task DeleteAsync(object? parameter)
    {
        var targets = GetTargets(parameter);
        var check = await services.Operations.RunAsync("בדיקת הפריטים למחיקה", targets, canStop: true, progress => CheckDeletion(targets, progress));
        if (check == null)
            return;

        var errors = check.Errors;
        if (check.Allowed.Count > 0)
        {
            if (!ConfirmDeletion(check))
                return;

            string title = check.Allowed.Count == 1 ? $"מחיקת {NameOf(check.Allowed[0])}" : $"מחיקת {check.Allowed.Count} פריטים";
            errors.AddRange(await services.Operations.RunAsync(title, check.Allowed, canStop: true, progress => DeleteAll(check, progress)));

            tab.Reload();
            CommandManager.InvalidateRequerySuggested();
        }

        if (errors.Count > 0)
            dialogs.Warn($"חלק מהפריטים לא נמחקו:\n\n{Summarize(errors)}", "מחיקה");
    }

    private sealed record DeletionCheck(List<string> Allowed, List<string> Errors, bool Recyclable, FolderContents Contents);

    private DeletionCheck? CheckDeletion(List<string> targets, OperationProgress progress)
    {
        var allowed = new List<string>();
        var errors = new List<string>();

        foreach (var target in targets)
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

        try
        {
            var contents = FolderInspector.CountContents(allowed.Where(Directory.Exists), progress);
            return new DeletionCheck(allowed, errors, allowed.All(services.Deletion.SupportsRecycleBin), contents);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private List<string> DeleteAll(DeletionCheck check, OperationProgress progress)
    {
        var errors = new List<string>();
        // Windows reports no progress for the recycle bin, so there only whole items are counted.
        progress.AddTotal(check.Recyclable ? check.Allowed.Count : check.Allowed.Count + check.Contents.Files + check.Contents.Folders, 0);

        foreach (var target in check.Allowed)
        {
            if (progress.IsCancelled)
                break;

            string? error;
            bool deleted;
            if (check.Recyclable)
            {
                progress.Begin(Path.GetFileName(target));
                deleted = services.Deletion.MoveToRecycleBin(target, out error);
                progress.Finish(0);
            }
            else
            {
                deleted = services.Deletion.DeletePermanently(target, out error, progress);
            }

            if (!deleted)
                errors.Add($"{Path.GetFileName(target)}: {error}");
        }

        return errors;
    }

    private bool ConfirmDeletion(DeletionCheck check)
    {
        string subject = check.Allowed.Count == 1 ? $"למחוק את הפריט?\n{check.Allowed[0]}" : $"למחוק {check.Allowed.Count} פריטים?";
        string details = string.Empty;
        if (check.Contents.HasFolders)
        {
            details = $"תוכן התיקיות: {check.Contents.Files} קבצים, {check.Contents.Folders} תתי-תיקיות.\n";
            if (check.Contents.Hidden > 0)
                details += $"שים לב: כולל {check.Contents.Hidden} פריטים מוסתרים או פריטי מערכת.\n";
            details += "\n";
        }

        string consequence = check.Recyclable
            ? "הפריטים יועברו לסל המיחזור."
            : "שים לב: אין בכונן זה סל מיחזור. המחיקה סופית ולא ניתן יהיה לשחזר!";

        return dialogs.Confirm($"{subject}\n\n{details}{consequence}", check.Recyclable ? "מחיקת פריטים" : "מחיקה לצמיתות", "מחיקה");
    }

    private async Task RenameAsync(object? parameter)
    {
        var targets = GetTargets(parameter);
        if (targets.Count != 1)
            return;

        string path = targets[0];
        string? denied = null;
        var kind = await services.Operations.RunAsync("בדיקת הקובץ", targets, canStop: false, _ =>
            !File.Exists(path) ? (bool?)null : policy.CanOpenFile(path, out denied));

        if (kind == null)
        {
            dialogs.Error("ניתן לשנות שם לקבצים בלבד.", "שינוי שם");
            return;
        }

        if (kind == false)
        {
            services.Audit.Log(AuditEventType.FileRenamed, path, denied, false);
            dialogs.Warn($"לא ניתן לשנות שם לקובץ זה:\n{denied}", "גישה נדחתה");
            return;
        }

        string currentName = Path.GetFileNameWithoutExtension(path);
        string? newName = dialogs.AskText("השם החדש (הסיומת נשמרת):", "שינוי שם", currentName);
        if (newName == null || string.Equals(newName, currentName, StringComparison.OrdinalIgnoreCase))
            return;

        string? error = await services.Operations.RunAsync($"שינוי שם של {NameOf(path)}", targets, canStop: false, _ =>
            services.Rename.Rename(path, newName, out var message) ? null : message);

        if (error == null)
            tab.Reload();
        else
            dialogs.Error($"שינוי השם נכשל:\n{error}", "שינוי שם");
    }

    private async Task CreateDirectoryAsync()
    {
        if (tab.TabPath is not { } parent || !dialogs.AskNewFolder(out var name))
            return;

        string? error = await services.Operations.RunAsync($"יצירת התיקייה {name}", new[] { parent }, canStop: false, _ =>
            services.Folders.Create(parent, name, out var message) ? null : message);

        if (error == null)
            tab.Reload();
        else
            dialogs.Error($"יצירת התיקייה נכשלה:\n{error}", "תיקייה חדשה");
    }

    private async Task EjectAsync(object? parameter)
    {
        string? drive = FindRemovableDrive(parameter);
        if (drive == null)
        {
            dialogs.Info("נא לבחור כונן נשלף להוצאה.", "הוצאת התקן");
            return;
        }

        if (services.Operations.IsUsing(drive))
        {
            dialogs.Warn("מתבצעת כרגע פעולת קבצים בכונן זה. המתן לסיומה או בטל אותה.", "הוצאת התקן USB");
            return;
        }

        // Folder watchers in every open tab hold the volume open and would fail the lock.
        services.Devices.NotifyDriveReleasing(drive);
        bool success;
        string message = string.Empty;
        try
        {
            success = await services.Operations.RunAsync($"הוצאת הכונן {NameOf(drive)}", Array.Empty<string>(), canStop: false, _ => services.Eject.EjectDrive(drive, out message));
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

    // The marks keep a Latin name intact inside a Hebrew title.
    private static string NameOf(string path)
    {
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        return $"\u200E{(name.Length == 0 ? path.TrimEnd('\\') : name)}\u200E";
    }

    private static string Summarize(List<string> errors)
    {
        string text = string.Join("\n", errors.Take(5));
        return errors.Count > 5 ? text + $"\nועוד {errors.Count - 5} שגיאות נוספות." : text;
    }
}
