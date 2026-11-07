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
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace LiteExplorer.MVVM.ViewModels;

internal class TabContentViewModel : ViewModel, IDisposable
{
    private const long NotApplicableSpaceSentinel = 12345667658675846L;

    #region Private fields

    private readonly BackgroundWorker worker;
    private readonly IPathPolicy pathPolicy;

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
                MessageBox.Show(reason, "׳’׳™׳©׳” ׳ ׳“׳—׳×׳”", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            TabPath = Path.GetFullPath(rawPath);
            OpenTabPath();
        }
        else if (File.Exists(rawPath))
        {
            if (!pathPolicy.CanOpenFile(rawPath, out var reason))
            {
                MessageBox.Show(reason, "׳’׳™׳©׳” ׳ ׳“׳—׳×׳”", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            try
            {
                var fullFilePath = Path.GetFullPath(rawPath);
                Process.Start(new ProcessStartInfo(fullFilePath) { UseShellExecute = true });
            }
            catch (Win32Exception ex)
            {
                MessageBox.Show(ex.Message, $"Error: {ex.NativeErrorCode}", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "׳©׳’׳™׳׳” ׳‘׳₪׳×׳™׳—׳× ׳”׳§׳•׳‘׳¥", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            MessageBox.Show($"׳”׳ ׳×׳™׳‘ ׳”׳׳‘׳•׳§׳© ׳׳™׳ ׳• ׳§׳™׳™׳ ׳׳• ׳©׳׳™׳ ׳• ׳ ׳’׳™׳©:\n{rawPath}", "׳©׳’׳™׳׳”", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        }
    }

    #endregion

    #region Delete

    public ICommand DeleteCmd { get; }

    private void OnDeletePath(object p)
    {
        string rawPath = p?.ToString()?.Trim();

        if (string.IsNullOrWhiteSpace(rawPath))
            return;

        if (!pathPolicy.CanDelete(rawPath, out var deleteReason))
        {
            MessageBox.Show(deleteReason, "׳©׳’׳™׳׳” ׳‘׳׳—׳™׳§׳”", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            return;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(rawPath);
        }
        catch
        {
            MessageBox.Show("׳ ׳×׳™׳‘ ׳‘׳׳×׳™ ׳×׳§׳™׳.", "׳©׳’׳™׳׳”", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            return;
        }

        bool isDirectory = Directory.Exists(fullPath);
        bool isFile = File.Exists(fullPath);

        if (!isDirectory && !isFile)
        {
            MessageBox.Show("׳”׳₪׳¨׳™׳˜ ׳׳׳—׳™׳§׳” ׳׳™׳ ׳• ׳§׳™׳™׳ ׳‘׳׳¢׳¨׳›׳×.", "׳©׳’׳™׳׳”", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
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
            confirmMessage = $"׳”׳׳ ׳׳×׳” ׳‘׳˜׳•׳— ׳©׳‘׳¨׳¦׳•׳ ׳ ׳׳׳—׳•׳§ ׳׳× ׳”׳×׳™׳§׳™׳™׳”:\n{fullPath}?\n\n׳₪׳™׳¨׳•׳˜ ׳×׳›׳•׳׳”:\nג€¢ ׳׳¡׳₪׳¨ ׳§׳‘׳¦׳™׳: {fileCount}\nג€¢ ׳×׳×׳™-׳×׳™׳§׳™׳•׳×: {dirCount}";
            if (hiddenCount > 0)
            {
                confirmMessage += "\n\nג ן¸ ׳©׳™׳ ׳׳‘: ׳”׳×׳™׳§׳™׳™׳” ׳׳›׳™׳׳” {hiddenCount} ׳₪׳¨׳™׳˜׳™׳ ׳׳•׳¡׳×׳¨׳™׳ ׳׳• ׳₪׳¨׳™׳˜׳™ ׳׳¢׳¨׳›׳×!";
            }
            confirmMessage += "\n\n׳”׳₪׳¨׳™׳˜׳™׳ ׳™׳•׳¢׳‘׳¨׳• ׳׳¡׳ ׳”׳׳™׳—׳–׳•׳¨.";
        }
        else
        {
            confirmMessage = $"׳”׳׳ ׳׳×׳” ׳‘׳˜׳•׳— ׳©׳‘׳¨׳¦׳•׳ ׳ ׳׳׳—׳•׳§ ׳׳× ׳”׳§׳•׳‘׳¥:\n{fullPath}?\n\n׳”׳§׳•׳‘׳¥ ׳™׳•׳¢׳‘׳¨ ׳׳¡׳ ׳”׳׳™׳—׳–׳•׳¨.";
        }

        MessageBoxResult result = MessageBox.Show(
            confirmMessage,
            "׳׳™׳©׳•׳¨ ׳׳—׳™׳§׳”",
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
            MessageBox.Show("׳”׳׳—׳™׳§׳” ׳‘׳•׳¦׳¢׳” ׳‘׳”׳¦׳׳—׳” (׳”׳•׳¢׳‘׳¨ ׳׳¡׳ ׳”׳׳™׳—׳–׳•׳¨).", "׳”׳׳—׳™׳§׳” ׳”׳¦׳׳™׳—׳”", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        }
        catch (Exception ex)
        {
            // Prompt user if Recycle Bin is unsupported on the target volume.
            var fallbackResult = MessageBox.Show(
                $"׳׳ ׳ ׳™׳×׳ ׳”׳™׳” ׳׳”׳¢׳‘׳™׳¨ ׳׳× ׳”׳₪׳¨׳™׳˜ ׳׳¡׳ ׳”׳׳™׳—׳–׳•׳¨ ({ex.Message}).\n׳”׳׳ ׳‘׳¨׳¦׳•׳ ׳ ׳׳׳—׳•׳§ ׳׳¦׳׳™׳×׳•׳× ׳׳× ׳”׳₪׳¨׳™׳˜?",
                "׳׳–׳”׳¨׳× ׳׳—׳™׳§׳” ׳׳¦׳׳™׳×׳•׳×",
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
                    MessageBox.Show("׳”׳׳—׳™׳§׳” ׳׳¦׳׳™׳×׳•׳× ׳‘׳•׳¦׳¢׳” ׳‘׳”׳¦׳׳—׳”.", "׳”׳׳—׳™׳§׳” ׳”׳¦׳׳™׳—׳”", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                }
                catch (Exception ex2)
                {
                    MessageBox.Show($"׳”׳׳—׳™׳§׳” ׳ ׳›׳©׳׳”: {ex2.Message}", "׳©׳’׳™׳׳”", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
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
            MessageBox.Show("׳ ׳™׳×׳ ׳׳©׳ ׳•׳× ׳©׳ ׳׳§׳‘׳¦׳™׳ ׳‘׳׳‘׳“.", "׳©׳’׳™׳׳”", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.No, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            return;
        }

        if (!pathPolicy.CanOpenFile(rawPath, out var checkReason))
        {
            MessageBox.Show($"׳׳ ׳ ׳™׳×׳ ׳׳©׳ ׳•׳× ׳©׳ ׳׳§׳•׳‘׳¥ ׳–׳”:\n{checkReason}", "׳’׳™׳©׳” ׳ ׳“׳—׳×׳”", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            return;
        }

        try
        {
            var fileInfo = new FileInfo(Path.GetFullPath(rawPath));
            string currentFileName = Path.GetFileNameWithoutExtension(fileInfo.FullName);
            string fileExtension = fileInfo.Extension;

            string newFileName = Microsoft.VisualBasic.Interaction.InputBox(
                "׳”׳›׳ ׳¡ ׳׳× ׳”׳©׳ ׳”׳—׳“׳© ׳׳§׳•׳‘׳¥:",
                "׳©׳™׳ ׳•׳™ ׳©׳ ׳”׳§׳•׳‘׳¥",
                currentFileName
            );

            if (string.IsNullOrWhiteSpace(newFileName))
                return;

            newFileName = newFileName.Trim();
            if (string.Equals(newFileName, currentFileName, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("׳©׳ ׳”׳§׳•׳‘׳¥ ׳׳ ׳©׳•׳ ׳”.", "׳©׳™׳ ׳•׳™ ׳©׳", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            if (newFileName.IndexOfAny(invalidChars) >= 0 ||
                newFileName.Contains('/') ||
                newFileName.Contains('\\') ||
                newFileName.Contains(".."))
            {
                MessageBox.Show("׳©׳ ׳”׳§׳•׳‘׳¥ ׳׳›׳™׳ ׳×׳•׳•׳™׳ ׳‘׳׳×׳™ ׳—׳•׳§׳™׳™׳ ׳׳• ׳ ׳™׳¡׳™׳•׳ ׳׳ ׳™׳•׳•׳˜ ׳ ׳×׳™׳‘.", "׳©׳’׳™׳׳”", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            string combinedName = newFileName + fileExtension;
            string newPath = Path.Combine(fileInfo.DirectoryName, combinedName);

            if (!string.Equals(Path.GetDirectoryName(newPath), fileInfo.DirectoryName, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("׳ ׳™׳¡׳™׳•׳ ׳׳¢׳§׳•׳£ ׳ ׳×׳™׳‘ (Path Traversal) ׳–׳•׳”׳” ׳•׳ ׳—׳¡׳.", "׳׳‘׳˜׳—׳”", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            if (!pathPolicy.CanRename(fileInfo.FullName, newPath, out var renameReason))
            {
                MessageBox.Show(renameReason, "׳©׳’׳™׳׳× ׳׳“׳™׳ ׳™׳•׳×", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            if (File.Exists(newPath))
            {
                MessageBox.Show("׳§׳•׳‘׳¥ ׳¢׳ ׳©׳ ׳–׳” ׳›׳‘׳¨ ׳§׳™׳™׳.", "׳©׳’׳™׳׳”", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            File.Move(fileInfo.FullName, newPath);

            MessageBox.Show("׳”׳©׳ ׳©׳•׳ ׳” ׳‘׳”׳¦׳׳—׳”.", "׳©׳™׳ ׳•׳™ ׳©׳", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            OpenTabPath();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"׳©׳™׳ ׳•׳™ ׳”׳©׳ ׳ ׳›׳©׳: {ex.Message}", "׳©׳’׳™׳׳”", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        }
    }

    #endregion

    #region ChangeDesign

    public ICommand ChangeDesign { get; }

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

        DeleteFiles(filesToDelete);
    }

    #endregion

    #region OpenAbout

    public ICommand OpenAboutCmd { get; }

    private void OnOpenAboutCmdExecuted(object p)
    {
        var aboutWindow = new about();
        aboutWindow.ShowDialog();
    }

    #endregion

    #endregion

    #region Constructor

    public TabContentViewModel(IPathPolicy pathPolicy = null)
    {
        this.pathPolicy = pathPolicy ?? App.Container?.GetService<IPathPolicy>() ?? new PathPolicy();

        worker = new BackgroundWorker
        {
            WorkerSupportsCancellation = true,
            WorkerReportsProgress = true
        };

        worker.DoWork += worker_DoWork;
        worker.ProgressChanged += worker_ProgressChanged;
        worker.RunWorkerCompleted += worker_RunWorkerCompleted;

        OpenCmd = new ActionCommand(OnOpenCmdExecuted);
        DeleteCmd = new ActionCommand(OnDeletePath);
        RenameCmd = new ActionCommand(OnRenameCmdExecuted);
        BackCmd = new ActionCommand(OnBackCmdExecuted, CanBackCmdExecute);
        BackHome = new ActionCommand(OnBackHomeExecuted);
        ResetOtzariaCmd = new ActionCommand(OnResetOtzariaCmdExecuted);
        OpenAboutCmd = new ActionCommand(OnOpenAboutCmdExecuted);

        OpenCmd.Execute(TabPath);
    }

    public void Dispose()
    {
        worker.DoWork -= worker_DoWork;
        worker.ProgressChanged -= worker_ProgressChanged;
        worker.RunWorkerCompleted -= worker_RunWorkerCompleted;
        worker.Dispose();
    }

    #endregion

    #region Private methods

    private void OpenTabPath()
    {
        if (worker.IsBusy)
        {
            worker.CancelAsync();
        }
        else
        {
            if (TabPath != null && !pathPolicy.CanNavigate(TabPath, out var reason))
            {
                MessageBox.Show(reason, "׳’׳™׳©׳” ׳ ׳“׳—׳×׳”", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                TabPath = null;
            }

            if (TabPath != null && Path.GetPathRoot(TabPath) == TabPath)
            {
                try
                {
                    var drive = new DriveInfo(TabPath);
                    TabTitle = GetDriveLabel(drive);
                }
                catch
                {
                    TabTitle = TabPath;
                }
            }
            else
            {
                TabTitle = string.IsNullOrEmpty(TabPath) ? "׳‘׳™׳×" : Path.GetFileName(TabPath);
            }

            FileSystemObjects.Clear();
            worker.RunWorkerAsync();
        }
    }

    private string GetDriveLabel(DriveInfo drive)
    {
        try
        {
            string label = (drive.IsReady && !string.IsNullOrEmpty(drive.VolumeLabel)) ? drive.VolumeLabel : "׳“׳™׳¡׳§ ׳׳§׳•׳׳™";
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
            return "׳“׳™׳¡׳§ ׳׳§׳•׳׳™";
        }
        else if (driveType == "Removable")
        {
            return "׳“׳™׳¡׳§ ׳ ׳©׳׳£ (USB)";
        }
        else
        {
            return driveType;
        }
    }

    private void worker_DoWork(object sender, DoWorkEventArgs e)
    {
        if (TabPath == null)
        {
            var visibleDrives = pathPolicy.GetAllowedDrives().ToList();
            var driveCount = visibleDrives.Count;
            int processedDrives = 0;

            foreach (var drive in visibleDrives)
            {
                if (worker.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

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

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        FileSystemObjects.Add(new FileSystemObject()
                        {
                            Image = FolderManager.GetImageSource(rootPath, ItemState.Undefined),
                            Name = driveLabel,
                            Path = drive.Name,
                            TotalSpace = totalSize,
                            FreeSpace = freeSpace,
                            Size = totalSize - freeSpace,
                            Format = format,
                            Type = driveType
                        });
                    }, DispatcherPriority.Background);
                }
                catch
                {
                }

                processedDrives++;
                if (driveCount > 0)
                    worker.ReportProgress((int)((double)processedDrives / driveCount * 100));
            }
        }
        else
        {
            try
            {
                if (!pathPolicy.CanNavigate(TabPath, out var navReason))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(navReason, "׳׳™׳ ׳’׳™׳©׳”", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                        TabPath = null;
                        OpenTabPath();
                    });
                    return;
                }

                var dirInfo = new DirectoryInfo(TabPath);
                var subDirs = dirInfo.EnumerateDirectories()
                    .Where(d => pathPolicy.CanListDirectory(d.FullName, out _))
                    .ToList();
                var files = dirInfo.EnumerateFiles()
                    .Where(f => pathPolicy.CanListFile(f.FullName, out _))
                    .ToList();

                var entryCount = subDirs.Count + files.Count;
                int processedFiles = 0;

                foreach (var directoryInfo in subDirs)
                {
                    if (worker.CancellationPending)
                    {
                        e.Cancel = true;
                        return;
                    }

                    var directory = directoryInfo.FullName;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        FileSystemObjects.Add(new FileSystemObject()
                        {
                            Image = FolderManager.GetImageSource(directory, ItemState.Undefined),
                            Name = directoryInfo.Name,
                            Path = directory,
                            Size = 0,
                            TotalSpace = NotApplicableSpaceSentinel,
                            FreeSpace = NotApplicableSpaceSentinel,
                            Format = directoryInfo.LastWriteTime.ToString()
                        });
                    }, DispatcherPriority.Background);

                    processedFiles++;
                    if (entryCount > 0)
                        worker.ReportProgress((int)((double)processedFiles / entryCount * 100));
                }

                foreach (var fileInfo in files)
                {
                    if (worker.CancellationPending)
                    {
                        e.Cancel = true;
                        return;
                    }

                    var file = fileInfo.FullName;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        FileSystemObjects.Add(new FileSystemObject()
                        {
                            Image = FileManager.GetImageSource(file),
                            Name = fileInfo.Name,
                            Path = file,
                            Size = fileInfo.Length,
                            Format = fileInfo.LastWriteTime.ToString(),
                            CreationTime = fileInfo.CreationTime.ToString(),
                            TotalSpace = NotApplicableSpaceSentinel,
                            FreeSpace = NotApplicableSpaceSentinel
                        });
                    }, DispatcherPriority.Background);

                    processedFiles++;
                    if (entryCount > 0)
                        worker.ReportProgress((int)((double)processedFiles / entryCount * 100));
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"׳”׳’׳™׳©׳” ׳ ׳“׳—׳×׳”:\n{ex.Message}", "׳׳™׳ ׳’׳™׳©׳”", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                    TabPath = null;
                    OpenTabPath();
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"׳©׳’׳™׳׳” ׳‘׳˜׳¢׳™׳ ׳× ׳”׳ ׳×׳™׳‘:\n{ex.Message}", "׳©׳’׳™׳׳”", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                    TabPath = null;
                    OpenTabPath();
                });
            }
        }
    }

    private void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
    {
        ProgressPercent = e.ProgressPercentage;
    }

    private void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        if (e.Cancelled) OpenTabPath();
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
            return $"׳׳ ׳ ׳™׳×׳ ׳”׳™׳” ׳׳׳—׳•׳§ ׳—׳׳§ ׳׳”׳§׳‘׳¦׳™׳:\n{string.Join("\n", failureList)}";
        }

        return "success";
    }

    #endregion
}