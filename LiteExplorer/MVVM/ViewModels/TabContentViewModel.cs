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
                MessageBox.Show(ex.Message, $"Error: {ex.NativeErrorCode}", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
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
        string path = p?.ToString();

        if (string.IsNullOrEmpty(path) || path.Length < 5)
        {
            MessageBox.Show("לא ניתן למחוק כוננים", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.No, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            return;
        }

        MessageBoxResult result = MessageBox.Show(
            $"האם אתה בטוח שאתה רוצה למחוק את {(Directory.Exists(path) ? "התיקיה" : "הקובץ")} במיקום:\n{path}?\nזוהי פעולה בלתי הפיכה!",
            "אישור מחיקה",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No,
            MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }

                OpenTabPath();
                MessageBox.Show("המחיקה בוצעה בהצלחה", "המחיקה הצליחה", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"המחיקה נכשלה: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show($"שינוי השם נכשל: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
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

        worker = new BackgroundWorker
        {
            WorkerSupportsCancellation = true,
            WorkerReportsProgress = true
        };

        worker.DoWork += worker_DoWork;
        worker.ProgressChanged += worker_ProgressChanged;
        worker.RunWorkerCompleted += worker_RunWorkerCompleted;

        OpenCmd = new ActionCommand(OnOpenCmdExecuted);
        DeleteCmd = new ActionCommand(onDeletePath);
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
                MessageBox.Show(reason, "גישה נדחתה", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
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
                TabTitle = string.IsNullOrEmpty(TabPath) ? "בית" : Path.GetFileName(TabPath);
            }

            FileSystemObjects.Clear();
            worker.RunWorkerAsync();
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
                        MessageBox.Show(navReason, "אין גישה", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
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
                            TotalSpace = 012345667658675846,
                            FreeSpace = 012345667658675846,
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
                            TotalSpace = 012345667658675846,
                            FreeSpace = 012345667658675846
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
                    MessageBox.Show($"הגישה נדחתה:\n{ex.Message}", "אין גישה", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                    TabPath = null;
                    OpenTabPath();
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"שגיאה בטעינת הנתיב:\n{ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
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
            return $"לא ניתן היה למחוק חלק מהקבצים:\n{string.Join("\n", failureList)}";
        }
        else
        {
            return "success";
        }
    }

    #endregion
}
