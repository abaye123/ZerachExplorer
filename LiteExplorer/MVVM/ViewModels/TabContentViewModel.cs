using LiteExplorer.Helpers;
using LiteExplorer.Helpers.Enums;
using LiteExplorer.Infrastructure.Commands;
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
using System.Windows.Input;
using System.Windows.Threading;

namespace LiteExplorer.MVVM.ViewModels;

internal class TabContentViewModel : ViewModel, IDisposable
{
    #region Private fields

    private readonly IPathPolicy pathPolicy;
    private CancellationTokenSource currentScanCts;
    private long currentScanId;
    private readonly object scanLock = new();

    #endregion

    #region Properties

    #region TabTitle

    private string tabTitle;

    public string TabTitle
    {
        get => tabTitle;
        set => SetValue(ref tabTitle, value);
    }

    #endregion

    #region TabPath

    private string tabPath;

    public string TabPath
    {
        get => tabPath;
        set
        {
            value = value?.Trim();
            if (value == "") value = null;
            SetValue(ref tabPath, value);
        }
    }

    #endregion

    #region TabCommand

    private string tabCommand;

    public string TabCommand
    {
        get => tabCommand;
        set => SetValue(ref tabCommand, value);
    }

    #endregion

    #region CurrentFileSystemObject

    private FileSystemObject currentFileSystemObject;

    public FileSystemObject CurrentFileSystemObject
    {
        get => currentFileSystemObject;
        set => SetValue(ref currentFileSystemObject, value);
    }

    #endregion

    #region ProgressPercent

    private double progressPercent;

    public double ProgressPercent
    {
        get => progressPercent;
        set => SetValue(ref progressPercent, value);
    }

    #endregion

    #region IsLoading

    private bool isLoading;

    public bool IsLoading
    {
        get => isLoading;
        set => SetValue(ref isLoading, value);
    }

    #endregion

    public ObservableCollection<FileSystemObject> FileSystemObjects { get; set; } = new();

    #endregion

    #region Commands

    #region Run

    public ICommand RunCmd { get; }

    #endregion

    #region Open

    public ICommand OpenCmd { get; }

    private void OnOpenCmdExecuted(object p)
    {
        string rawPath = p?.ToString()?.Trim();

        // If no path is provided, navigate to Home (Drives list)
        if (string.IsNullOrEmpty(rawPath))
        {
            TabPath = null;
            OpenTabPath();
            return;
        }

        // 3.2 Verify all navigation and open operations against PathPolicy
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

    #endregion

    #region Delete

    public ICommand DeleteCmd { get; }

    private void onDeletePath(object p)
    {
        string rawPath = p?.ToString()?.Trim();

        if (string.IsNullOrWhiteSpace(rawPath))
            return;

        // 3.6 Robust validation: Block drive deletion and verify path policy
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

        // Count contents and check for hidden/system files before recursive delete
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
                // Inaccessible subfolder or count error - proceed cautiously
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

        // 3.6 Send to Recycle Bin instead of permanent recursive wipeout
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
            MessageBox.Show("המחיקה בוצעה בהצלחה (הועבר לסל המיחזור).", "המחיקה הצליחה", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        }
        catch (Exception ex)
        {
            CrashLogger.LogException(ex, $"Recycle bin delete failed for: {fullPath}");

            // If drive does not support Recycle Bin (e.g. FAT32 USB), ask explicit confirmation
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

    #endregion

    #region Rename

    public ICommand RenameCmd { get; }

    private void OnRenameCmdExecuted(object p)
    {
        string rawPath = p?.ToString()?.Trim();

        if (string.IsNullOrEmpty(rawPath) || !File.Exists(rawPath))
        {
            MessageBox.Show("ניתן לשנות שם לקבצים בלבד.", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.No, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            return;
        }

        // 3.5 Verify that user is allowed to access/rename this file
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

            // Prompt user for new file name
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

            // 3.5 Prevent Path Traversal in Rename
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

            // Verify directory remains identical
            if (!string.Equals(Path.GetDirectoryName(newPath), fileInfo.DirectoryName, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("ניסיון לעקוף נתיב (Path Traversal) זוהה ונחסם.", "אבטחה", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            // Validate against policy
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
        }
        catch (Exception ex)
        {
            CrashLogger.LogException(ex, $"Rename failed for {rawPath}");
            MessageBox.Show($"שינוי השם נכשל: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        }
    }

    #endregion

    #region CalculateFolderSize

    public ICommand CalculateFolderSizeCmd { get; }

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
                // Return accumulated size on unexpected error
            }

            return size;
        }, token);
    }

    #endregion

    #region Back

    public ICommand BackCmd { get; }

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
            if (pathPolicy.CanNavigate(parent, out _))
            {
                TabPath = parent;
            }
            else
            {
                TabPath = null;
            }
        }

        OpenTabPath();
    }

    #endregion

    #region BackHome

    public ICommand BackHome { get; }

    private void OnBackHomeExecuted(object p)
    {
        TabPath = null;
        OpenTabPath();
    }

    #endregion

    #region ResetOtzaria

    public ICommand ResetOtzariaCmd { get; }

    private void OnResetOtzariaCmdExecuted(object p)
    {
        var filesToDelete = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"com.example\otzaria\tabs.isar"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"com.example\otzaria\history.isar"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"com.example\otzaria\bookmarks.isar")
        };

        string result = DeleteFiles(filesToDelete);
        Console.WriteLine(result);
    }

    #endregion

    #region OpenAbout

    public ICommand OpenAboutCmd { get; }

    private void OnOpenAboutCmdExecuted(object p)
    {
        about aboutForm = new about();
        aboutForm.ShowDialog();
    }

    #endregion

    #endregion

    #region Constructor

    public TabContentViewModel(IPathPolicy pathPolicy = null)
    {
        this.pathPolicy = pathPolicy ?? App.Container?.GetService<IPathPolicy>() ?? new PathPolicy();

        OpenCmd = new ActionCommand(OnOpenCmdExecuted);
        DeleteCmd = new ActionCommand(onDeletePath);
        RenameCmd = new ActionCommand(OnRenameCmdExecuted);
        BackCmd = new ActionCommand(OnBackCmdExecuted, CanBackCmdExecute);
        BackHome = new ActionCommand(OnBackHomeExecuted);
        CalculateFolderSizeCmd = new ActionCommand(OnCalculateFolderSizeExecuted);
        ResetOtzariaCmd = new ActionCommand(OnResetOtzariaCmdExecuted);
        OpenAboutCmd = new ActionCommand(OnOpenAboutCmdExecuted);

        OpenCmd.Execute(TabPath);
    }

    public void Dispose()
    {
        lock (scanLock)
        {
            currentScanCts?.Cancel();
            currentScanCts?.Dispose();
            currentScanCts = null;
        }
    }

    #endregion

    #region Private methods

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

        FileSystemObjects.Clear();
        ProgressPercent = 0;
        IsLoading = true;

        _ = LoadDirectoryContentsAsync(targetPath, scanId, token);
    }

    private async Task LoadDirectoryContentsAsync(string targetPath, long scanId, CancellationToken token)
    {
        try
        {
            await Task.Run(async () =>
            {
                if (targetPath == null)
                {
                    // 3.7 Whitelist drive enumeration
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
                            // Skip drive if error occurs
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
                    // Verify navigation permission inside worker task
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
                            Size = 0, // Lazy/On-demand folder size to avoid freezing UI
                            TotalSpace = 012345667658675846,
                            FreeSpace = 012345667658675846,
                            Format = directoryInfo.LastWriteTime.ToString(),
                            LastWriteTime = directoryInfo.LastWriteTime.ToString(),
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
                            TotalSpace = 012345667658675846,
                            FreeSpace = 012345667658675846,
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

                    // Flush remaining items in batch
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
            // Operation cleanly cancelled by a newer request or tab navigation
            return;
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
            // Ignore cancellation
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
        var successList = new List<string>();
        var failureList = new List<string>();

        foreach (var filePath in filePaths)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    successList.Add(filePath);
                }
            }
            catch (Exception ex)
            {
                failureList.Add($"{filePath} (Error: {ex.Message})");
            }
        }

        if (failureList.Any())
        {
            return $"לא ניתן היה למחוק חלק מהקבצים:\n{string.Join("\n", failureList)}";
        }
        else
        {
            return "success";
        }
    }

    #endregion
}
