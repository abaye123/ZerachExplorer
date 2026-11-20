using LiteExplorer.Helpers;

using LiteExplorer.Helpers.Enums;

using LiteExplorer.Helpers.WinAPI;

using LiteExplorer.Infrastructure.Commands;

using LiteExplorer.Infrastructure.Converters;

using LiteExplorer.MVVM.Models;

using LiteExplorer.MVVM.ViewModels.Base;

using LiteExplorer.MVVM.Views.Windows;

using LiteExplorer.Services;

using Microsoft.Extensions.DependencyInjection;

using System;

using System.Collections.Generic;

using System.Collections.ObjectModel;

using System.ComponentModel;

using System.Diagnostics;

using System.IO;

using System.Linq;

using System.Threading;

using System.Threading.Tasks;

using System.Windows;

using System.Windows.Data;

using System.Windows.Input;

using System.Windows.Media.Imaging;

using System.Windows.Threading;



namespace LiteExplorer.MVVM.ViewModels;



internal class TabContentViewModel : ViewModel, IDisposable

{

    private const long NotApplicableSpaceSentinel = 12345667658675846L;



    #region Private fields



    private readonly IPathPolicy pathPolicy;

    private readonly IDriveEjectService driveEjectService;

    private readonly object scanLock = new();

    private CancellationTokenSource currentScanCts;

    private long currentScanId;



    private static readonly List<string> InternalClipboard = new();

    private static bool IsClipboardCut = false;

    private readonly List<FileSystemObject> selectedFileSystemObjects = new();



    private FileSystemWatcher currentWatcher;

    private readonly DispatcherTimer refreshDebounceTimer;



    #endregion



    #region Properties



    private string tabTitle;

    public string TabTitle

    {

        get => tabTitle;

        set => SetValue(ref tabTitle, value);

    }



    private string tabPath;

    public string TabPath

    {

        get => tabPath;

        set

        {

            if (SetValue(ref tabPath, value))

            {

                TabCommand = tabPath;
                UpdateBreadcrumbs(tabPath);

                InvalidateCommandStates();

            }

        }

    }



    private string tabCommand;

    public string TabCommand

    {

        get => tabCommand;

        set => SetValue(ref tabCommand, value);

    }



    private FileSystemObject currentFileSystemObject;

    public FileSystemObject CurrentFileSystemObject

    {

        get => currentFileSystemObject;

        set

        {

            if (SetValue(ref currentFileSystemObject, value))

            {

                InvalidateCommandStates();

                if (IsPreviewPaneVisible)

                {

                    LoadPreview(currentFileSystemObject);

                }

            }

        }

    }



    private double progressPercent;

    public double ProgressPercent

    {

        get => progressPercent;

        set => SetValue(ref progressPercent, value);

    }



    private bool isLoading;

    public bool IsLoading

    {

        get => isLoading;

        set => SetValue(ref isLoading, value);

    }



    public ObservableCollection<FileSystemObject> FileSystemObjects { get; set; } = new();

    public IReadOnlyList<FileSystemObject> SelectedFileSystemObjects => selectedFileSystemObjects;



    public ObservableCollection<BreadcrumbItem> Breadcrumbs { get; } = new();



    private bool isAddressBarEditing;

    public bool IsAddressBarEditing

    {

        get => isAddressBarEditing;

        set => SetValue(ref isAddressBarEditing, value);

    }



    private string searchText = string.Empty;

    public string SearchText

    {

        get => searchText;

        set

        {

            if (SetValue(ref searchText, value))

            {

                ApplyFilter();

            }

        }

    }



    public SortColumn CurrentSortColumn { get; private set; } = SortColumn.Name;

    public ListSortDirection CurrentSortDirection { get; private set; } = ListSortDirection.Ascending;



    public FilePreviewInfo PreviewInfo { get; } = new();



    private bool isPreviewPaneVisible;

    public bool IsPreviewPaneVisible

    {

        get => isPreviewPaneVisible;

        set

        {

            if (SetValue(ref isPreviewPaneVisible, value))

            {

                if (value)

                {

                    LoadPreview(CurrentFileSystemObject);

                }

                else

                {

                    PreviewInfo.Clear();

                }

            }

        }

    }



    #endregion



    #region Commands



    public ICommand RunCmd { get; }

    public ICommand OpenCmd { get; }

    public ICommand DeleteCmd { get; }

    public ICommand RenameCmd { get; }

    public ICommand CalculateFolderSizeCmd { get; }

    public ICommand BackCmd { get; }

    public ICommand BackHome { get; }

    public ICommand ResetOtzariaCmd { get; }

    public ICommand OpenAboutCmd { get; }

    public ICommand CopyCmd { get; }

    public ICommand CutCmd { get; }

    public ICommand PasteCmd { get; }

    public ICommand CreateDirectoryCmd { get; }

    public ICommand EjectDriveCmd { get; }

    public ICommand NavigateBreadcrumbCmd { get; }

    public ICommand ToggleAddressBarEditCmd { get; }

    public ICommand ClearSearchCmd { get; }

    public ICommand TogglePreviewPaneCmd { get; }

    public ICommand RefreshCmd { get; }



    #endregion



    #region Constructor & Disposal



    public TabContentViewModel(IPathPolicy pathPolicy = null, IDriveEjectService driveEjectService = null)

    {

        this.pathPolicy = pathPolicy ?? App.Container?.GetService<IPathPolicy>() ?? new PathPolicy();

        this.driveEjectService = driveEjectService ?? App.Container?.GetService<IDriveEjectService>() ?? new DriveEjectService(this.pathPolicy);



        OpenCmd = new ActionCommand(OnOpenCmdExecuted);

        DeleteCmd = new ActionCommand(OnDeletePath, CanDeleteExecute);

        RenameCmd = new ActionCommand(OnRenameCmdExecuted, CanRenameExecute);

        BackCmd = new ActionCommand(OnBackCmdExecuted, CanBackCmdExecute);

        BackHome = new ActionCommand(OnBackHomeExecuted);

        CalculateFolderSizeCmd = new ActionCommand(OnCalculateFolderSizeExecuted);

        ResetOtzariaCmd = new ActionCommand(OnResetOtzariaCmdExecuted);

        OpenAboutCmd = new ActionCommand(OnOpenAboutCmdExecuted);

        CopyCmd = new ActionCommand(OnCopyCmdExecuted, CanCopyExecute);

        CutCmd = new ActionCommand(OnCutCmdExecuted, CanCutExecute);

        PasteCmd = new ActionCommand(OnPasteCmdExecuted, CanPasteExecute);

        CreateDirectoryCmd = new ActionCommand(OnCreateDirectoryCmdExecuted, CanCreateDirectoryExecute);

        EjectDriveCmd = new ActionCommand(OnEjectDriveCmdExecuted, CanEjectDriveExecute);

        NavigateBreadcrumbCmd = new ActionCommand(OnNavigateBreadcrumbExecuted);

        ToggleAddressBarEditCmd = new ActionCommand(_ => IsAddressBarEditing = !IsAddressBarEditing);

        ClearSearchCmd = new ActionCommand(_ => SearchText = string.Empty);

        TogglePreviewPaneCmd = new ActionCommand(_ => IsPreviewPaneVisible = !IsPreviewPaneVisible);

        RefreshCmd = new ActionCommand(_ => OpenCmd.Execute(TabPath));



        refreshDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };

        refreshDebounceTimer.Tick += (s, e) =>

        {

            refreshDebounceTimer.Stop();

            OpenCmd.Execute(TabPath);

        };



        DeviceNotificationService.Instance.DeviceChanged += OnDeviceNotification;



        UpdateBreadcrumbs(TabPath);

        OpenCmd.Execute(TabPath);

    }



    public void Dispose()

    {

        DeviceNotificationService.Instance.DeviceChanged -= OnDeviceNotification;

        StopWatcher();

        refreshDebounceTimer?.Stop();



        lock (scanLock)

        {

            currentScanCts?.Cancel();

            currentScanCts?.Dispose();

            currentScanCts = null;

        }

    }

    #endregion

    #region Command State Evaluation (CanExecute)

    private bool CanCopyExecute(object p) => HasSelectedOrCurrentItem(p);

    private bool CanCutExecute(object p) => HasSelectedOrCurrentItem(p);

    private bool CanPasteExecute(object p)
    {
        if (string.IsNullOrEmpty(TabPath) || !Directory.Exists(TabPath))
            return false;

        return HasClipboardData();
    }

    private bool CanDeleteExecute(object p) => HasSelectedOrCurrentItem(p);

    private bool CanRenameExecute(object p)
    {
        if (p != null) return true;
        if (selectedFileSystemObjects.Count == 1) return true;
        return selectedFileSystemObjects.Count == 0 && CurrentFileSystemObject != null && !string.IsNullOrEmpty(CurrentFileSystemObject.Path);
    }

    private bool CanCreateDirectoryExecute(object p)
    {
        return !string.IsNullOrEmpty(TabPath) && Directory.Exists(TabPath);
    }

    private bool CanEjectDriveExecute(object p)
    {
        if (CurrentFileSystemObject != null && !string.IsNullOrEmpty(CurrentFileSystemObject.Path))
            return true;
        if (!string.IsNullOrEmpty(TabPath))
            return true;
        return selectedFileSystemObjects.Count > 0;
    }

    private bool HasSelectedOrCurrentItem(object p)
    {
        if (p != null && !string.IsNullOrEmpty(p.ToString()))
            return true;

        if (selectedFileSystemObjects.Count > 0)
            return true;

        return CurrentFileSystemObject != null && !string.IsNullOrEmpty(CurrentFileSystemObject.Path);
    }

    private static bool HasClipboardData()
    {
        lock (InternalClipboard)
        {
            if (InternalClipboard.Count > 0)
                return true;
        }

        try
        {
            return Clipboard.ContainsFileDropList();
        }
        catch
        {
            return false;
        }
    }

    private static void InvalidateCommandStates()
    {
        try
        {
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
            }
            else
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
        catch
        {
        }
    }

    #endregion

    #region Selection

    public void SetSelectedItems(IEnumerable<FileSystemObject> items)
    {
        selectedFileSystemObjects.Clear();
        if (items != null)
        {
            selectedFileSystemObjects.AddRange(items);
        }
        InvalidateCommandStates();
    }

    private List<string> GetSelectedOrCurrentPaths(object commandParameter = null)
    {
        var paramPath = commandParameter?.ToString()?.Trim();
        if (!string.IsNullOrEmpty(paramPath))
        {
            return new List<string> { paramPath };
        }

        if (selectedFileSystemObjects.Count > 0)
        {
            return selectedFileSystemObjects
                .Where(x => !string.IsNullOrEmpty(x.Path))
                .Select(x => x.Path)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (!string.IsNullOrEmpty(CurrentFileSystemObject?.Path))
        {
            return new List<string> { CurrentFileSystemObject.Path };
        }

        return new List<string>();
    }

    #endregion

    #region File Transfer Handlers (Copy, Cut, Paste)

    private void OnCopyCmdExecuted(object p)
    {
        var paths = GetSelectedOrCurrentPaths(p);
        if (paths.Count == 0) return;

        lock (InternalClipboard)
        {
            InternalClipboard.Clear();
            InternalClipboard.AddRange(paths);
            IsClipboardCut = false;
        }

        try
        {
            var collection = new System.Collections.Specialized.StringCollection();
            collection.AddRange(paths.ToArray());
            Clipboard.SetFileDropList(collection);
        }
        catch
        {
        }

        InvalidateCommandStates();
    }

    private void OnCutCmdExecuted(object p)
    {
        var paths = GetSelectedOrCurrentPaths(p);
        if (paths.Count == 0) return;

        lock (InternalClipboard)
        {
            InternalClipboard.Clear();
            InternalClipboard.AddRange(paths);
            IsClipboardCut = true;
        }

        try
        {
            var collection = new System.Collections.Specialized.StringCollection();
            collection.AddRange(paths.ToArray());
            Clipboard.SetFileDropList(collection);
        }
        catch
        {
        }

        InvalidateCommandStates();
    }

    private async void OnPasteCmdExecuted(object p)
    {
        if (string.IsNullOrEmpty(TabPath) || !Directory.Exists(TabPath))
        {
            MessageBox.Show("לא ניתן להדביק פריטים במסך הראשי. אנא בחר כונן או תיקייה תחילה.", "הדבקה", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            return;
        }

        if (!pathPolicy.CanNavigate(TabPath, out var navReason))
        {
            MessageBox.Show(navReason, "גישה נדחתה", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            return;
        }

        List<string> sourcesToPaste;
        bool isCut;

        lock (InternalClipboard)
        {
            if (InternalClipboard.Count > 0)
            {
                sourcesToPaste = InternalClipboard.ToList();
                isCut = IsClipboardCut;
            }
            else
            {
                sourcesToPaste = new List<string>();
                isCut = false;
                try
                {
                    if (Clipboard.ContainsFileDropList())
                    {
                        var dropList = Clipboard.GetFileDropList();
                        foreach (var item in dropList)
                        {
                            if (!string.IsNullOrWhiteSpace(item))
                                sourcesToPaste.Add(item);
                        }
                    }
                }
                catch
                {
                }
            }
        }

        if (sourcesToPaste.Count == 0)
            return;

        string targetDir = TabPath;
        var errors = new List<string>();
        ConflictResolutionAction? globalResolution = null;

        IsLoading = true;
        try
        {
            await Task.Run(() =>
            {
                foreach (var source in sourcesToPaste)
                {
                    bool isFile = File.Exists(source);
                    bool isDir = Directory.Exists(source);

                    if (!isFile && !isDir)
                    {
                        errors.Add($"הפריט '{source}' אינו קיים במערכת.");
                        continue;
                    }

                    if (isFile)
                    {
                        if (isCut)
                        {
                            if (!pathPolicy.CanMoveFile(source, targetDir, out var reason))
                            {
                                errors.Add($"{Path.GetFileName(source)}: {reason}");
                                continue;
                            }
                        }
                        else
                        {
                            if (!pathPolicy.CanCopyFile(source, targetDir, out var reason))
                            {
                                errors.Add($"{Path.GetFileName(source)}: {reason}");
                                continue;
                            }
                        }

                        string fileName = Path.GetFileName(source);
                        string destFile = Path.Combine(targetDir, fileName);

                        if (File.Exists(destFile))
                        {
                            ConflictResolutionAction action;
                            if (globalResolution.HasValue)
                            {
                                action = globalResolution.Value;
                            }
                            else
                            {
                                ConflictResolutionResult dialogResult = null;
                                Application.Current?.Dispatcher?.Invoke(() =>
                                {
                                    dialogResult = ConflictResolutionDialog.Prompt(
                                        Application.Current?.MainWindow,
                                        fileName,
                                        destFile,
                                        source);
                                });

                                if (dialogResult == null || dialogResult.Action == ConflictResolutionAction.Cancel)
                                    break;

                                if (dialogResult.ApplyToAll)
                                    globalResolution = dialogResult.Action;

                                action = dialogResult.Action;
                            }

                            if (action == ConflictResolutionAction.Skip)
                                continue;

                            if (action == ConflictResolutionAction.KeepBoth)
                                destFile = GenerateUniqueFilePath(targetDir, fileName);
                        }

                        try
                        {
                            if (isCut)
                            {
                                File.Move(source, destFile, overwrite: true);
                            }
                            else
                            {
                                File.Copy(source, destFile, overwrite: true);
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"{fileName}: {ex.Message}");
                        }
                    }
                    else
                    {
                        if (!pathPolicy.CanTransferDirectory(source, targetDir, isCut, out var reason))
                        {
                            errors.Add($"{Path.GetFileName(source)}: {reason}");
                            continue;
                        }

                        string dirName = Path.GetFileName(source.TrimEnd('\\', '/'));
                        string destSubDir = Path.Combine(targetDir, dirName);

                        try
                        {
                            if (isCut)
                            {
                                MoveDirectory(source, destSubDir);
                            }
                            else
                            {
                                CopyDirectoryRecursive(source, destSubDir);
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"{dirName}: {ex.Message}");
                        }
                    }
                }
            });

            if (isCut)
            {
                lock (InternalClipboard)
                {
                    InternalClipboard.Clear();
                }
                try
                {
                    Clipboard.Clear();
                }
                catch
                {
                }
            }

            OpenTabPath();

            if (errors.Count > 0)
            {
                string msg = $"חלק מהקבצים לא הועברו:\n\n{string.Join("\n", errors.Take(5))}";
                if (errors.Count > 5)
                    msg += $"\nועוד {errors.Count - 5} שגיאות נוספות.";

                MessageBox.Show(msg, "הודעת העברה", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            }
        }
        finally
        {
            IsLoading = false;
            InvalidateCommandStates();
        }
    }

    private static string GenerateUniqueFilePath(string destinationDirectory, string fileName)
    {
        string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);
        int counter = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(destinationDirectory, $"{nameWithoutExt} ({counter}){ext}");
            counter++;
        } while (File.Exists(candidate) || Directory.Exists(candidate));

        return candidate;
    }

    private static void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }
        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            string destSub = Path.Combine(destDir, Path.GetFileName(subDir));
            CopyDirectoryRecursive(subDir, destSub);
        }
    }

    private static void MoveDirectory(string sourceDir, string destDir)
    {
        try
        {
            Directory.Move(sourceDir, destDir);
        }
        catch (IOException)
        {
            CopyDirectoryRecursive(sourceDir, destDir);
            Directory.Delete(sourceDir, recursive: true);
        }
    }

    #endregion

    #region Directory Creation & Device Eject Handlers

    private void OnCreateDirectoryCmdExecuted(object p)
    {
        if (string.IsNullOrEmpty(TabPath) || !Directory.Exists(TabPath))
        {
            MessageBox.Show("לא ניתן ליצור תיקייה במסך הראשי. אנא בחר כונן או תיקייה תחילה.", "יצירת תיקייה", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            return;
        }

        if (!pathPolicy.CanNavigate(TabPath, out var navReason))
        {
            MessageBox.Show(navReason, "גישה נדחתה", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            return;
        }

        if (NewFolderDialog.Prompt(Application.Current?.MainWindow, TabPath, pathPolicy, out string createdFolderName))
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(TabPath, createdFolderName));
                OpenTabPath();
            }
            catch (Exception ex)
            {
                CrashLogger.LogException(ex, $"Failed to create folder '{createdFolderName}' in '{TabPath}'");
                MessageBox.Show($"יצירת התיקייה נכשלה: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            }
        }
    }

    private void OnEjectDriveCmdExecuted(object p)
    {
        string targetDrive = null;
        if (CurrentFileSystemObject != null && !string.IsNullOrEmpty(CurrentFileSystemObject.Path))
        {
            targetDrive = Path.GetPathRoot(CurrentFileSystemObject.Path);
        }
        else if (!string.IsNullOrEmpty(TabPath))
        {
            targetDrive = Path.GetPathRoot(TabPath);
        }
        else if (selectedFileSystemObjects.Count > 0)
        {
            targetDrive = Path.GetPathRoot(selectedFileSystemObjects[0].Path);
        }

        if (string.IsNullOrEmpty(targetDrive))
        {
            MessageBox.Show("נא לבחור כונן נשלף להוצאה.", "הוצאת התקן", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            return;
        }

        bool success = driveEjectService.EjectDrive(targetDrive, out string message);
        MessageBox.Show(
            message,
            "הוצאת התקן USB",
            MessageBoxButton.OK,
            success ? MessageBoxImage.Information : MessageBoxImage.Warning,
            MessageBoxResult.OK,
            MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);

        if (success)
        {
            BackHome.Execute(null);
        }
    }

    #endregion

    #region Navigation & Execution Handlers

    private void OnOpenCmdExecuted(object p)
    {
        string rawPath = p?.ToString()?.Trim();

        // Navigate to Drives list if no path is specified.
        if (string.IsNullOrEmpty(rawPath))
        {
            TabPath = null;
            OpenTabPath();
            return;
        }

        if (Directory.Exists(rawPath))
        {
            if (!pathPolicy.CanNavigate(rawPath, out var reason))
            {
                MessageBox.Show(reason, "גישה נדחתה", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            TabPath = Path.GetFullPath(rawPath);
            OpenTabPath();
        }
        else if (File.Exists(rawPath))
        {
            if (!pathPolicy.CanOpenFile(rawPath, out var reason))
            {
                MessageBox.Show(reason, "גישה נדחתה", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            try
            {
                var fullFilePath = Path.GetFullPath(rawPath);
                Process.Start(new ProcessStartInfo(fullFilePath) { UseShellExecute = true });
            }
            catch (Win32Exception ex)
            {
                CrashLogger.LogException(ex, $"ShellExecute failed for file: {rawPath}");
                MessageBox.Show(ex.Message, $"Error: {ex.NativeErrorCode}", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                CrashLogger.LogException(ex, $"Failed to open file: {rawPath}");
                MessageBox.Show(ex.Message, "שגיאה בפתיחת הקובץ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            MessageBox.Show($"הנתיב המבוקש אינו קיים או שאינו נגיש:\n{rawPath}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        }
    }

    private void OnDeletePath(object p)
    {
        var targets = GetSelectedOrCurrentPaths(p);
        if (targets.Count == 0) return;

        if (targets.Count > 1)
        {
            var confirmMulti = MessageBox.Show(
                $"האם אתה בטוח שברצונך להעביר {targets.Count} פריטים שנבחרו לסל המיחזור?",
                "אישור מחיקת פריטים",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No,
                MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);

            if (confirmMulti != MessageBoxResult.Yes) return;

            var multiErrors = new List<string>();
            foreach (var target in targets)
            {
                if (!pathPolicy.CanDelete(target, out var reason))
                {
                    multiErrors.Add($"{Path.GetFileName(target)}: {reason}");
                    continue;
                }

                try
                {
                    if (Directory.Exists(target))
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                            target,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    }
                    else if (File.Exists(target))
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                            target,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    }
                }
                catch (Exception ex)
                {
                    multiErrors.Add($"{Path.GetFileName(target)}: {ex.Message}");
                }
            }

            OpenTabPath();
            InvalidateCommandStates();

            if (multiErrors.Count > 0)
            {
                MessageBox.Show($"שגיאה במחיקת חלק מהפריטים:\n{string.Join("\n", multiErrors.Take(5))}", "שגיאה במחיקה", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            }
            return;
        }

        string rawPath = targets[0];
        if (!pathPolicy.CanDelete(rawPath, out var deleteReason))
        {
            MessageBox.Show(deleteReason, "שגיאה במחיקה", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            return;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(rawPath);
        }
        catch
        {
            MessageBox.Show("נתיב בלתי תקין.", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            return;
        }

        bool isDirectory = Directory.Exists(fullPath);
        bool isFile = File.Exists(fullPath);

        if (!isDirectory && !isFile)
        {
            MessageBox.Show("הפריט למחיקה אינו קיים במערכת.", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            return;
        }

        int fileCount = 0;
        int dirCount = 0;
        int hiddenCount = 0;

        if (isDirectory)
        {
            try
            {
                var dirInfo = new DirectoryInfo(fullPath);
                foreach (var fsi in dirInfo.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
                {
                    if ((fsi.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                    {
                        hiddenCount++;
                    }

                    if ((fsi.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                        dirCount++;
                    else
                        fileCount++;
                }
            }
            catch
            {
            }
        }

        string confirmMessage;
        if (isDirectory)
        {
            confirmMessage = $"האם אתה בטוח שברצונך למחוק את התיקייה:\n{fullPath}?\n\nפירוט תכולה:\n• מספר קבצים: {fileCount}\n• תתי-תיקיות: {dirCount}";
            if (hiddenCount > 0)
            {
                confirmMessage += $"\n\n⚠️ שים לב: התיקייה מכילה {hiddenCount} פריטים מוסתרים או פריטי מערכת!";
            }
            confirmMessage += "\n\nהפריטים יועברו לסל המיחזור.";
        }
        else
        {
            confirmMessage = $"האם אתה בטוח שברצונך למחוק את הקובץ:\n{fullPath}?\n\nהקובץ יועבר לסל המיחזור.";
        }

        MessageBoxResult result = MessageBox.Show(
            confirmMessage,
            "אישור מחיקה",
            MessageBoxButton.YesNo,
            hiddenCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Question,
            MessageBoxResult.No,
            MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            if (isDirectory)
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                    fullPath,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            }
            else
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                    fullPath,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            }

            OpenTabPath();
            InvalidateCommandStates();
            MessageBox.Show("המחיקה בוצעה בהצלחה (הועבר לסל המיחזור).", "המחיקה הצליחה", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        }
        catch (Exception ex)
        {
            CrashLogger.LogException(ex, $"Recycle bin delete failed for: {fullPath}");

            var fallbackResult = MessageBox.Show(
                $"לא ניתן היה להעביר את הפריט לסל המיחזור ({ex.Message}).\nהאם ברצונך למחוק לצמיתות את הפריט?",
                "אזהרת מחיקה לצמיתות",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No,
                MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);

            if (fallbackResult == MessageBoxResult.Yes)
            {
                try
                {
                    if (isDirectory)
                        Directory.Delete(fullPath, true);
                    else
                        File.Delete(fullPath);

                    OpenTabPath();
                    InvalidateCommandStates();
                    MessageBox.Show("המחיקה לצמיתות בוצעה בהצלחה.", "המחיקה הצליחה", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                }
                catch (Exception ex2)
                {
                    CrashLogger.LogException(ex2, $"Permanent delete failed for: {fullPath}");
                    MessageBox.Show($"המחיקה נכשלה: {ex2.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                }
            }
        }
    }

    private void OnRenameCmdExecuted(object p)
    {
        string rawPath = p?.ToString()?.Trim();

        if (string.IsNullOrEmpty(rawPath))
        {
            var targets = GetSelectedOrCurrentPaths();
            if (targets.Count == 1)
                rawPath = targets[0];
        }

        if (string.IsNullOrEmpty(rawPath) || !File.Exists(rawPath))
        {
            MessageBox.Show("ניתן לשנות שם לקבצים בלבד.", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.No, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            return;
        }

        if (!pathPolicy.CanOpenFile(rawPath, out var checkReason))
        {
            MessageBox.Show($"לא ניתן לשנות שם לקובץ זה:\n{checkReason}", "גישה נדחתה", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            return;
        }

        try
        {
            var fileInfo = new FileInfo(Path.GetFullPath(rawPath));
            string currentFileName = Path.GetFileNameWithoutExtension(fileInfo.FullName);
            string fileExtension = fileInfo.Extension;

            string newFileName = Microsoft.VisualBasic.Interaction.InputBox(
                "הכנס את השם החדש לקובץ:",
                "שינוי שם הקובץ",
                currentFileName
            );

            if (string.IsNullOrWhiteSpace(newFileName))
                return;

            newFileName = newFileName.Trim();
            if (string.Equals(newFileName, currentFileName, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("שם הקובץ לא שונה.", "שינוי שם", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            if (newFileName.IndexOfAny(invalidChars) >= 0 ||
                newFileName.Contains('/') ||
                newFileName.Contains('\\') ||
                newFileName.Contains(".."))
            {
                MessageBox.Show("שם הקובץ מכיל תווים בלתי חוקיים או ניסיון לניווט נתיב.", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            string combinedName = newFileName + fileExtension;
            string newPath = Path.Combine(fileInfo.DirectoryName, combinedName);

            if (!string.Equals(Path.GetDirectoryName(newPath), fileInfo.DirectoryName, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("ניסיון לעקוף נתיב (Path Traversal) זוהה ונחסם.", "אבטחה", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            if (!pathPolicy.CanRename(fileInfo.FullName, newPath, out var renameReason))
            {
                MessageBox.Show(renameReason, "שגיאת מדיניות", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            if (File.Exists(newPath))
            {
                MessageBox.Show("קובץ עם שם זה כבר קיים.", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            File.Move(fileInfo.FullName, newPath);

            MessageBox.Show("השם שונה בהצלחה.", "שינוי שם", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            OpenTabPath();
            InvalidateCommandStates();
        }
        catch (Exception ex)
        {
            CrashLogger.LogException(ex, $"Rename failed for {rawPath}");
            MessageBox.Show($"שינוי השם נכשל: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        }
    }

    private async void OnCalculateFolderSizeExecuted(object p)
    {
        var targetObj = (p as FileSystemObject) ?? CurrentFileSystemObject;
        if (targetObj == null || !targetObj.IsDirectory || string.IsNullOrWhiteSpace(targetObj.Path))
            return;

        if (targetObj.IsCalculatingSize)
            return;

        targetObj.IsCalculatingSize = true;

        try
        {
            long totalBytes = await CalculateDirectorySizeAsync(targetObj.Path);
            targetObj.Size = totalBytes;
        }
        catch (Exception ex)
        {
            CrashLogger.LogMessage($"Failed to calculate folder size for {targetObj.Path}: {ex.Message}", "WARN");
        }
        finally
        {
            targetObj.IsCalculatingSize = false;
        }
    }

    public static async Task<long> CalculateDirectorySizeAsync(string directoryPath, CancellationToken token = default)
    {
        return await Task.Run(() =>
        {
            long size = 0;
            try
            {
                var dir = new DirectoryInfo(directoryPath);
                if (!dir.Exists) return 0;

                var stack = new Stack<DirectoryInfo>();
                stack.Push(dir);

                while (stack.Count > 0)
                {
                    token.ThrowIfCancellationRequested();
                    var currentDir = stack.Pop();

                    try
                    {
                        foreach (var file in currentDir.EnumerateFiles())
                        {
                            token.ThrowIfCancellationRequested();
                            size += file.Length;
                        }
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (DirectoryNotFoundException) { }
                    catch (PathTooLongException) { }
                    catch (IOException) { }

                    try
                    {
                        foreach (var subDir in currentDir.EnumerateDirectories())
                        {
                            token.ThrowIfCancellationRequested();
                            stack.Push(subDir);
                        }
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (DirectoryNotFoundException) { }
                    catch (PathTooLongException) { }
                    catch (IOException) { }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
            }

            return size;
        }, token);
    }

    private bool CanBackCmdExecute(object p) => TabPath != null;

    private void OnBackCmdExecuted(object p)
    {
        if (TabPath == null) return;

        var parent = Path.GetDirectoryName(TabPath);
        if (string.IsNullOrEmpty(parent))
        {
            TabPath = null;
        }
        else
        {
            TabPath = pathPolicy.CanNavigate(parent, out _) ? parent : null;
        }

        OpenTabPath();
    }

    private void OnBackHomeExecuted(object p)
    {
        TabPath = null;
        OpenTabPath();
    }

    private void OnResetOtzariaCmdExecuted(object p)
    {
        var filesToDelete = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"com.example\otzaria\tabs.isar"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"com.example\otzaria\history.isar"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"com.example\otzaria\bookmarks.isar")
        };

        DeleteFiles(filesToDelete);
    }

    private void OnOpenAboutCmdExecuted(object p)
    {
        var aboutWindow = new about();
        aboutWindow.ShowDialog();
    }

    #endregion

    #region Private Directory Loading

    private void OpenTabPath()
    {
        lock (scanLock)
        {
            currentScanCts?.Cancel();
            currentScanCts?.Dispose();
            currentScanCts = new CancellationTokenSource();
        }

        long scanId = Interlocked.Increment(ref currentScanId);
        var token = currentScanCts.Token;

        string targetPath = TabPath;

        if (targetPath != null && !pathPolicy.CanNavigate(targetPath, out var reason))
        {
            MessageBox.Show(reason, "גישה נדחתה", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            TabPath = null;
            targetPath = null;
        }

        if (targetPath != null && Path.GetPathRoot(targetPath) == targetPath)
        {
            try
            {
                var drive = new DriveInfo(targetPath);
                TabTitle = GetDriveLabel(drive);
            }
            catch
            {
                TabTitle = targetPath;
            }
        }
        else
        {
            TabTitle = string.IsNullOrEmpty(targetPath) ? "בית" : Path.GetFileName(targetPath);
        }

        UpdateBreadcrumbs(targetPath);
        StartWatcher(targetPath);

        FileSystemObjects.Clear();
        ProgressPercent = 0;
        IsLoading = true;

        _ = LoadDirectoryContentsAsync(targetPath, scanId, token);
        InvalidateCommandStates();
    }

    private async Task LoadDirectoryContentsAsync(string targetPath, long scanId, CancellationToken token)
    {
        try
        {
            await Task.Run(async () =>
            {
                if (targetPath == null)
                {
                    var visibleDrives = pathPolicy.GetAllowedDrives().ToList();
                    var driveCount = visibleDrives.Count;
                    int processedDrives = 0;
                    var driveBatch = new List<FileSystemObject>();

                    foreach (var drive in visibleDrives)
                    {
                        token.ThrowIfCancellationRequested();

                        try
                        {
                            var rootPath = drive.RootDirectory.FullName;
                            var driveLabel = GetDriveLabel(drive);
                            var driveType = GetDriveFormat(drive);
                            long totalSize = 0;
                            long freeSpace = 0;
                            string format = "";

                            if (drive.IsReady)
                            {
                                totalSize = drive.TotalSize;
                                freeSpace = drive.TotalFreeSpace;
                                format = drive.DriveFormat.ToString();
                            }

                            driveBatch.Add(new FileSystemObject
                            {
                                Image = FolderManager.GetImageSource(rootPath, ItemState.Undefined),
                                Name = driveLabel,
                                Path = drive.Name,
                                TotalSpace = totalSize,
                                FreeSpace = freeSpace,
                                Size = totalSize - freeSpace,
                                Format = format,
                                Type = driveType,
                                IsDirectory = true
                            });
                        }
                        catch
                        {
                        }

                        processedDrives++;
                        if (driveCount > 0)
                        {
                            int progress = (int)((double)processedDrives / driveCount * 100);
                            await DispatchOnUiThreadAsync(() => ProgressPercent = progress, token);
                        }
                    }

                    await DispatchOnUiThreadAsync(() =>
                    {
                        if (scanId != Interlocked.Read(ref currentScanId) || token.IsCancellationRequested)
                            return;

                        foreach (var item in driveBatch)
                            FileSystemObjects.Add(item);
                    }, token);
                }
                else
                {
                    if (!pathPolicy.CanNavigate(targetPath, out var navReason))
                    {
                        await DispatchOnUiThreadAsync(() =>
                        {
                            if (scanId != Interlocked.Read(ref currentScanId)) return;
                            MessageBox.Show(navReason, "אין גישה", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                            TabPath = null;
                            OpenTabPath();
                        }, token);
                        return;
                    }

                    var dirInfo = new DirectoryInfo(targetPath);
                    if (!dirInfo.Exists)
                    {
                        await DispatchOnUiThreadAsync(() =>
                        {
                            if (scanId != Interlocked.Read(ref currentScanId)) return;
                            MessageBox.Show("התיקייה המבוקשת אינה קיימת או שהוסרה.", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                            TabPath = null;
                            OpenTabPath();
                        }, token);
                        return;
                    }

                    var subDirs = dirInfo.EnumerateDirectories()
                        .Where(d => pathPolicy.CanListDirectory(d.FullName, out _))
                        .ToList();
                    var files = dirInfo.EnumerateFiles()
                        .Where(f => pathPolicy.CanListFile(f.FullName, out _))
                        .ToList();

                    int totalCount = subDirs.Count + files.Count;
                    int processedCount = 0;
                    const int BatchSize = 64;
                    var batch = new List<FileSystemObject>(BatchSize);

                    foreach (var directoryInfo in subDirs)
                    {
                        token.ThrowIfCancellationRequested();

                        var directory = directoryInfo.FullName;
                        batch.Add(new FileSystemObject
                        {
                            Image = FolderManager.GetImageSource(directory, ItemState.Undefined),
                            Name = directoryInfo.Name,
                            Path = directory,
                            Size = 0,
                            TotalSpace = NotApplicableSpaceSentinel,
                            FreeSpace = NotApplicableSpaceSentinel,
                            Format = directoryInfo.LastWriteTime.ToString(),
                            LastWriteTime = directoryInfo.LastWriteTime.ToString(),
                            RawLastWriteTime = directoryInfo.LastWriteTime,
                            IsDirectory = true
                        });

                        processedCount++;

                        if (batch.Count >= BatchSize)
                        {
                            var batchToSend = batch.ToList();
                            batch.Clear();
                            int currentProgress = totalCount > 0 ? (int)((double)processedCount / totalCount * 100) : 0;

                            await DispatchOnUiThreadAsync(() =>
                            {
                                if (scanId != Interlocked.Read(ref currentScanId) || token.IsCancellationRequested)
                                    return;

                                foreach (var item in batchToSend)
                                    FileSystemObjects.Add(item);

                                ProgressPercent = currentProgress;
                            }, token);
                        }
                    }

                    foreach (var fileInfo in files)
                    {
                        token.ThrowIfCancellationRequested();

                        var file = fileInfo.FullName;
                        batch.Add(new FileSystemObject
                        {
                            Image = FileManager.GetImageSource(file),
                            Name = fileInfo.Name,
                            Path = file,
                            Size = fileInfo.Length,
                            Format = fileInfo.LastWriteTime.ToString(),
                            CreationTime = fileInfo.CreationTime.ToString(),
                            LastWriteTime = fileInfo.LastWriteTime.ToString(),
                            RawLastWriteTime = fileInfo.LastWriteTime,
                            TotalSpace = NotApplicableSpaceSentinel,
                            FreeSpace = NotApplicableSpaceSentinel,
                            IsDirectory = false
                        });

                        processedCount++;

                        if (batch.Count >= BatchSize)
                        {
                            var batchToSend = batch.ToList();
                            batch.Clear();
                            int currentProgress = totalCount > 0 ? (int)((double)processedCount / totalCount * 100) : 0;

                            await DispatchOnUiThreadAsync(() =>
                            {
                                if (scanId != Interlocked.Read(ref currentScanId) || token.IsCancellationRequested)
                                    return;

                                foreach (var item in batchToSend)
                                    FileSystemObjects.Add(item);

                                ProgressPercent = currentProgress;
                            }, token);
                        }
                    }

                    if (batch.Count > 0)
                    {
                        var remainingBatch = batch.ToList();
                        batch.Clear();

                        await DispatchOnUiThreadAsync(() =>
                        {
                            if (scanId != Interlocked.Read(ref currentScanId) || token.IsCancellationRequested)
                                return;

                            foreach (var item in remainingBatch)
                                FileSystemObjects.Add(item);
                        }, token);
                    }
                }
            }, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex) when (ex is IOException or DriveNotFoundException or DirectoryNotFoundException)
        {
            CrashLogger.LogMessage($"Device or directory disconnected when navigating to {targetPath}: {ex.Message}", "WARN");
            await DispatchOnUiThreadAsync(() =>
            {
                if (scanId != Interlocked.Read(ref currentScanId)) return;
                MessageBox.Show($"הכונן או התיקייה אינם זמינים (יתכן שההתקן נותק):\n{ex.Message}", "שגיאה בגישה לכונן", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                TabPath = null;
                OpenTabPath();
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            CrashLogger.LogMessage($"Unauthorized access when navigating to {targetPath}: {ex.Message}", "WARN");
            await DispatchOnUiThreadAsync(() =>
            {
                if (scanId != Interlocked.Read(ref currentScanId)) return;
                MessageBox.Show($"הגישה נדחתה:\n{ex.Message}", "אין גישה", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                TabPath = null;
                OpenTabPath();
            });
        }
        catch (Exception ex)
        {
            CrashLogger.LogException(ex, $"Failed to load path: {targetPath}");
            await DispatchOnUiThreadAsync(() =>
            {
                if (scanId != Interlocked.Read(ref currentScanId)) return;
                MessageBox.Show($"שגיאה בטעינת הנתיב:\n{ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                TabPath = null;
                OpenTabPath();
            });
        }
        finally
        {
            if (scanId == Interlocked.Read(ref currentScanId))
            {
                await DispatchOnUiThreadAsync(() =>
                {
                    IsLoading = false;
                    ProgressPercent = 100;
                    ApplySorting();
                    ApplyFilter();
                    if (IsPreviewPaneVisible)
                    {
                        LoadPreview(CurrentFileSystemObject);
                    }
                });
            }
        }
    }

    private static async Task DispatchOnUiThreadAsync(Action action, CancellationToken token = default)
    {
        if (Application.Current?.Dispatcher == null || Application.Current.Dispatcher.CheckAccess())
        {
            action();
            return;
        }

        try
        {
            await Application.Current.Dispatcher.InvokeAsync(action, DispatcherPriority.Background, token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private string GetDriveLabel(DriveInfo drive)
    {
        try
        {
            string label = (drive.IsReady && !string.IsNullOrEmpty(drive.VolumeLabel)) ? drive.VolumeLabel : "דיסק מקומי";
            return $"{label} ({drive.Name})";
        }
        catch
        {
            return drive.Name;
        }
    }

    private string GetDriveFormat(DriveInfo drive)
    {
        var driveType = drive.DriveType.ToString();

        if (driveType == "Fixed")
        {
            return "דיסק מקומי";
        }
        else if (driveType == "Removable")
        {
            return "דיסק נשלף (USB)";
        }
        else
        {
            return driveType;
        }
    }

    private static string DeleteFiles(List<string> filePaths)
    {
        var failureList = new List<string>();

        foreach (var filePath in filePaths)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                failureList.Add($"{filePath} (Error: {ex.Message})");
            }
        }

        return failureList.Any()
            ? $"לא ניתן היה למחוק חלק מהקבצים:\n{string.Join("\n", failureList)}"
            : "success";
    }

    #endregion

    #region Phase 4 Helpers: Breadcrumbs, Filter, Sort, Preview, Auto-Refresh



    private void UpdateBreadcrumbs(string path)

    {

        Breadcrumbs.Clear();

        Breadcrumbs.Add(new BreadcrumbItem

        {

            Name = "בית",

            Path = string.Empty,

            IsLast = string.IsNullOrEmpty(path)

        });



        if (string.IsNullOrEmpty(path))

            return;



        string root = Path.GetPathRoot(path);

        if (string.IsNullOrEmpty(root))

            return;



        string rootLabel = root;

        try

        {

            var drive = new DriveInfo(root);

            rootLabel = GetDriveLabel(drive);

        }

        catch { }



        bool isRootOnly = string.Equals(root.TrimEnd('\\', '/'), path.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);



        Breadcrumbs.Add(new BreadcrumbItem

        {

            Name = rootLabel,

            Path = root,

            IsLast = isRootOnly

        });



        if (isRootOnly)

            return;



        string sub = path.Substring(root.Length).Trim('\\', '/');

        if (string.IsNullOrEmpty(sub))

            return;



        string[] parts = sub.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

        string currentAccumulated = root.TrimEnd('\\', '/');



        for (int i = 0; i < parts.Length; i++)

        {

            currentAccumulated = Path.Combine(currentAccumulated, parts[i]);

            Breadcrumbs.Add(new BreadcrumbItem

            {

                Name = parts[i],

                Path = currentAccumulated,

                IsLast = (i == parts.Length - 1)

            });

        }

    }



    private void OnNavigateBreadcrumbExecuted(object p)

    {

        string path = p as string;

        OpenCmd.Execute(path);

    }



    private void ApplyFilter()

    {

        var view = CollectionViewSource.GetDefaultView(FileSystemObjects);

        if (view == null) return;



        if (string.IsNullOrWhiteSpace(searchText))

        {

            view.Filter = null;

        }

        else

        {

            string query = searchText.Trim();

            view.Filter = item =>

            {

                if (item is FileSystemObject fso && fso.Name != null)

                {

                    return fso.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

                }

                return true;

            };

        }

    }



    public void SortByColumn(string columnTag)

    {

        if (!Enum.TryParse<SortColumn>(columnTag, true, out var col))

            return;



        if (CurrentSortColumn == col)

        {

            CurrentSortDirection = CurrentSortDirection == ListSortDirection.Ascending

                ? ListSortDirection.Descending

                : ListSortDirection.Ascending;

        }

        else

        {

            CurrentSortColumn = col;

            CurrentSortDirection = ListSortDirection.Ascending;

        }



        ApplySorting();

        OnPropertyChanged(nameof(CurrentSortColumn));

        OnPropertyChanged(nameof(CurrentSortDirection));

    }



    private void ApplySorting()

    {

        if (CollectionViewSource.GetDefaultView(FileSystemObjects) is ListCollectionView view)

        {

            view.CustomSort = new FileSystemObjectComparer(CurrentSortColumn, CurrentSortDirection);

        }

    }



    private void StartWatcher(string path)

    {

        StopWatcher();



        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))

            return;



        try

        {

            currentWatcher = new FileSystemWatcher(path)

            {

                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,

                IncludeSubdirectories = false,

                EnableRaisingEvents = true

            };

            currentWatcher.Created += OnDirectoryChanged;

            currentWatcher.Deleted += OnDirectoryChanged;

            currentWatcher.Renamed += OnDirectoryRenamed;

            currentWatcher.Changed += OnDirectoryChanged;

            currentWatcher.Error += OnWatcherError;

        }

        catch (Exception ex)

        {

            CrashLogger.LogMessage($"FileSystemWatcher could not start on {path}: {ex.Message}", "INFO");

        }

    }



    private void StopWatcher()

    {

        if (currentWatcher != null)

        {

            try

            {

                currentWatcher.EnableRaisingEvents = false;

                currentWatcher.Created -= OnDirectoryChanged;

                currentWatcher.Deleted -= OnDirectoryChanged;

                currentWatcher.Renamed -= OnDirectoryRenamed;

                currentWatcher.Changed -= OnDirectoryChanged;

                currentWatcher.Error -= OnWatcherError;

                currentWatcher.Dispose();

            }

            catch { }

            finally

            {

                currentWatcher = null;

            }

        }

    }



    private void OnDirectoryChanged(object sender, FileSystemEventArgs e) => TriggerDebouncedRefresh();

    private void OnDirectoryRenamed(object sender, RenamedEventArgs e) => TriggerDebouncedRefresh();



    private void OnWatcherError(object sender, ErrorEventArgs e)

    {

        StopWatcher();

    }



    private void TriggerDebouncedRefresh()

    {

        Application.Current?.Dispatcher?.InvokeAsync(() =>

        {

            refreshDebounceTimer.Stop();

            refreshDebounceTimer.Start();

        });

    }



    private void OnDeviceNotification(object sender, EventArgs e)

    {

        Application.Current?.Dispatcher?.InvokeAsync(() =>

        {

            if (string.IsNullOrEmpty(TabPath))

            {

                OpenCmd.Execute(null);

            }

            else

            {

                try

                {

                    string root = Path.GetPathRoot(TabPath);

                    if (string.IsNullOrEmpty(root) || !Directory.Exists(root) || !new DriveInfo(root).IsReady)

                    {

                        MessageBox.Show("הכונן נותק מהמחשב. חוזר לתצוגת הבית.", "התקן נותק", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);

                        BackHome.Execute(null);

                    }

                }

                catch

                {

                    BackHome.Execute(null);

                }

            }

        });

    }



    private async void LoadPreview(FileSystemObject item)

    {

        if (item == null || string.IsNullOrEmpty(item.Path))

        {

            PreviewInfo.Clear();

            return;

        }



        PreviewInfo.FileName = item.Name;

        PreviewInfo.FilePath = item.Path;

        PreviewInfo.LastModifiedFormatted = item.LastWriteTime;

        PreviewInfo.IconPreview = item.Image;



        if (item.IsDirectory)

        {

            PreviewInfo.Type = PreviewType.Directory;

            PreviewInfo.FileSizeFormatted = item.Size > 0 ? BytesToNormalSize.FormatBytes(item.Size) : "תיקייה";

            PreviewInfo.StatusMessage = "תיקייה";

            PreviewInfo.TextContent = null;

            PreviewInfo.ImagePreview = null;

            return;

        }



        PreviewInfo.FileSizeFormatted = BytesToNormalSize.FormatBytes(item.Size);

        string ext = Path.GetExtension(item.Path)?.ToLowerInvariant() ?? string.Empty;



        // Text files

        if (ext is ".txt" or ".csv" or ".log" or ".json" or ".xml" or ".ini" or ".md" or ".config" or ".yml" or ".yaml")

        {

            PreviewInfo.IsLoading = true;

            PreviewInfo.Type = PreviewType.Text;

            PreviewInfo.ImagePreview = null;

            try

            {

                string text = await Task.Run(() =>

                {

                    using var stream = new FileStream(item.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                    using var reader = new StreamReader(stream);

                    char[] buffer = new char[65536];

                    int charsRead = reader.Read(buffer, 0, buffer.Length);

                    return new string(buffer, 0, charsRead);

                });

                PreviewInfo.TextContent = text;

                PreviewInfo.StatusMessage = $"מוצגים {text.Length} תווים ראשונים";

            }

            catch (Exception ex)

            {

                PreviewInfo.StatusMessage = "שגיאה בקריאת הקובץ: " + ex.Message;

                PreviewInfo.Type = PreviewType.Document;

            }

            finally

            {

                PreviewInfo.IsLoading = false;

            }

            return;

        }



        // Image files

        if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".ico")

        {

            PreviewInfo.IsLoading = true;

            PreviewInfo.Type = PreviewType.Image;

            PreviewInfo.TextContent = null;

            try

            {

                var bmp = await Task.Run(() =>

                {

                    var img = new BitmapImage();

                    img.BeginInit();

                    img.CacheOption = BitmapCacheOption.OnLoad;

                    img.UriSource = new Uri(item.Path);

                    img.DecodePixelWidth = 800;

                    img.EndInit();

                    img.Freeze();

                    return img;

                });

                PreviewInfo.ImagePreview = bmp;

                PreviewInfo.StatusMessage = "תמונה";

            }

            catch

            {

                PreviewInfo.StatusMessage = "לא ניתן לטעון תצוגה מקדימה לתמונה";

                PreviewInfo.Type = PreviewType.Document;

            }

            finally

            {

                PreviewInfo.IsLoading = false;

            }

            return;

        }



        // Office & PDF documents

        PreviewInfo.Type = PreviewType.Document;

        PreviewInfo.TextContent = null;

        PreviewInfo.ImagePreview = null;

        PreviewInfo.StatusMessage = "קובץ מסמך (דאבל קליק לפתיחה ביישום ברירת מחדל)";

    }



    #endregion



}

