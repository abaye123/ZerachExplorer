using LiteExplorer.Helpers;
using LiteExplorer.Helpers.Enums;
using LiteExplorer.Infrastructure.Commands;
using LiteExplorer.MVVM.Models;
using LiteExplorer.MVVM.ViewModels.Base;
using System;
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

    //private void OnRunCmdExecuted(object p) => Process.Start(new ProcessStartInfo("cmd", $"/k {p}") { WorkingDirectory = TabPath ?? Environment.SystemDirectory });

    #endregion

    #region Open

    string[] allowedExtensions = { ".doc", ".docx", ".xls", ".xlsx", ".pdf", ".txt", ".csv", ".onetoc2", ".one" };

    public ICommand OpenCmd { get; }

    private void OnOpenCmdExecuted(object p)
    {
        string path = p?.ToString();

        if (path is null || Directory.Exists(path))
        {
            TabPath = path;

            OpenTabPath();
        }
        else if (File.Exists(path))
        {
            try
            {
                var fileInfo = new FileInfo(path);

                if (Array.IndexOf(allowedExtensions, fileInfo.Extension.ToLower()) >= 0)
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show("סוג הקובץ אינו נתמך.", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
                }

            }
            catch (Win32Exception ex)
            {
                MessageBox.Show(ex.Message, $"Error: {ex.NativeErrorCode}", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            //Process.Start(new ProcessStartInfo($"https://www.google.com/?q={Uri.EscapeDataString(path)}") { UseShellExecute = true });
            MessageBox.Show("ERROR", $"Error: {path}", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public ICommand DeleteCmd { get; }

    private void onDeletePath(object p)
    {
        string path = p?.ToString();

        if (string.IsNullOrEmpty(path) || path.Length < 5)
        {
            MessageBox.Show("לא ניתן למחוק כוננים", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        //MessageBoxResult result = MessageBox.Show($"האם אתה בטוח שאתה רוצה למחוק את {(Directory.Exists(path) ? "התיקיה" : "הקובץ")} במיקום:\n{path}?", "אישור מחיקה", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        MessageBoxResult result = MessageBox.Show(
            $"האם אתה בטוח שאתה רוצה למחוק את {(Directory.Exists(path) ? "התיקיה" : "הקובץ")} במיקום:\n{path}?",
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
                    Directory.Delete(path, true); // true to delete recursively
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }

                OpenTabPath();
                MessageBox.Show("המחיקה בוצעה בהצלחה", "מחיקה הצליחה", MessageBoxButton.OK, MessageBoxImage.Information);
                
            }
            catch (Exception ex)
            {
                MessageBox.Show($"המחיקה נכשלה: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public ICommand ChangeDesign { get; }


    #endregion

    #region Back

    public ICommand BackCmd { get; }

    private bool CanBackCmdExecute(object p) => TabPath != null;

    private void OnBackCmdExecuted(object p)
    {
        TabPath = Path.GetDirectoryName(TabPath);
        OpenTabPath();
    }


    public ICommand BackHome { get; }

    private void OnBackHomeExecuted(object p)
    {
        TabPath = "";
        OpenTabPath();
    }

    #endregion

    #endregion

    #region Constructor

    public TabContentViewModel()
    {
        worker = new BackgroundWorker
        {
            WorkerSupportsCancellation = true,
            WorkerReportsProgress = true
        };

        worker.DoWork += worker_DoWork;
        worker.ProgressChanged += worker_ProgressChanged;
        worker.RunWorkerCompleted += worker_RunWorkerCompleted;

        //RunCmd = new ActionCommand(OnRunCmdExecuted);
        OpenCmd = new ActionCommand(OnOpenCmdExecuted);
        DeleteCmd = new ActionCommand(onDeletePath);
        BackCmd = new ActionCommand(OnBackCmdExecuted, CanBackCmdExecute);
        BackHome = new ActionCommand(OnBackHomeExecuted);
        //ChangeDesign = new ActionCommand(OnRunCmdExecuted);

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
            worker.CancelAsync();
        else
        {
            TabTitle = TabPath != null && Path.GetPathRoot(TabPath) == TabPath
            ? GetDriveLabel(new DriveInfo(TabPath))
            : Path.GetFileName(TabPath);
            FileSystemObjects.Clear();
            worker.RunWorkerAsync();
        }
    }

    private string GetDriveLabel(DriveInfo drive) => $"{(drive.VolumeLabel != "" ? drive.VolumeLabel : "Local drive")} ({drive.Name})";

    private void worker_DoWork(object sender, DoWorkEventArgs e)
    {
        if (TabPath == null)
        {
            var driveCount = Directory.GetLogicalDrives().Length;
            int processedDrives = 0;

            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.Name != "C:\\") // hide view c drive
                {
                    if (worker.CancellationPending)
                    {
                        e.Cancel = true;
                        return;
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        FileSystemObjects.Add(new FileSystemObject()
                        {
                            Image = FolderManager.GetImageSource(drive.RootDirectory.FullName, ItemState.Undefined),
                            Name = GetDriveLabel(drive),
                            Path = drive.Name,
                            TotalSpace = drive.TotalSize,
                            FreeSpace = drive.TotalFreeSpace,
                            Size = drive.TotalSize - drive.TotalFreeSpace,
                            Format = drive.DriveFormat,
                            Type = drive.DriveType.ToString()
                        });

                        //worker.ReportProgress((int)((double)FileSystemObjects.Count / (driveCount) * 100));
                    }, DispatcherPriority.Background);
                }
                processedDrives++;
                worker.ReportProgress((int)((double)processedDrives / driveCount * 100));
            }
        }
        else
        {
            try
            {
                var entryCount = new DirectoryInfo(TabPath).EnumerateFileSystemInfos().Count();
                if (IsNotOnDriveC(TabPath) == true)
                {
                    int processedFiles = 0;

                    foreach (var directory in Directory.EnumerateDirectories(TabPath))
                    {
                        if (worker.CancellationPending)
                        {
                            e.Cancel = true;
                            return;
                        }

                        // יצירת אובייקט DirectoryInfo עבור התיקייה הנוכחית
                        var directoryInfo = new DirectoryInfo(directory);

                        // בדיקה אם התיקייה מוסתרת
                        if ((directoryInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                        {
                            processedFiles++;
                            worker.ReportProgress((int)((double)processedFiles / entryCount * 100));
                            continue; // דילוג על הקובץ אם הוא מוסתר
                        }

                        // הוספת האובייקט לרשימה של FileSystemObjects
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            FileSystemObjects.Add(new FileSystemObject()
                            {
                                Image = FolderManager.GetImageSource(directory, ItemState.Undefined),
                                Name = Path.GetFileName(directory),
                                Path = directory,
                                Size = 0
                            });

                            //worker.ReportProgress((int)((double)FileSystemObjects.Count / entryCount * 100));
                        }, DispatcherPriority.Background);

                        processedFiles++;
                        worker.ReportProgress((int)((double)processedFiles / entryCount * 100));
                    }


                    foreach (var file in Directory.EnumerateFiles(TabPath))
                    {
                        if (worker.CancellationPending)
                        {
                            e.Cancel = true;
                            return;
                        }

                        var fileInfo = new FileInfo(file);

                        if ((fileInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                        {
                            processedFiles++;
                            worker.ReportProgress((int)((double)processedFiles / entryCount * 100));
                            continue;
                        }


                        //if (Array.IndexOf(allowedExtensions, fileInfo.Extension.ToLower()) < 0)
                        //{
                            //processedFiles++;
                            //worker.ReportProgress((int)((double)processedFiles / entryCount * 100));
                            //continue;
                        //}


                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            FileSystemObjects.Add(new FileSystemObject()
                            {
                                Image = FileManager.GetImageSource(file),
                                Name = Path.GetFileName(file),
                                Path = file,
                                Size = fileInfo.Length
                            });
                        }, DispatcherPriority.Background);

                        processedFiles++;
                        worker.ReportProgress((int)((double)processedFiles / entryCount * 100));
                    }

                }

            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show(ex.Message, "Access denied", MessageBoxButton.OK, MessageBoxImage.Error);
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

    public bool IsNotOnDriveC(string path)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("Path cannot be null or empty", nameof(path));

        return !(path.StartsWith("C:", StringComparison.OrdinalIgnoreCase));
    }


    #endregion
}
