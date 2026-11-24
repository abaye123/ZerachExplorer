using LiteExplorer.Helpers;
using LiteExplorer.Infrastructure.Commands;
using LiteExplorer.MVVM.Models;
using LiteExplorer.MVVM.ViewModels.Base;
using LiteExplorer.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace LiteExplorer.MVVM.ViewModels;

/// <summary>One explorer tab: where it is, what it shows and how it is sorted and filtered.</summary>
internal sealed class TabContentViewModel : ViewModel, IExplorerTab, IDisposable
{
    private readonly ExplorerServices services;
    private readonly IPathPolicy policy;
    private readonly IDialogService dialogs;
    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private readonly FolderWatcher watcher;
    private readonly SidebarViewModel sidebar;
    private readonly PreviewViewModel preview;
    private readonly FileOperationsViewModel operations;
    // Kept so that WPF does not discard the sort and filter while the tab is hidden.
    private readonly ListCollectionView itemsView;
    private readonly List<FileSystemObject> selectedItems = new();

    private CancellationTokenSource scan = new();
    private string? loadedPath;
    private bool hasLoaded;
    private bool disposed;

    private string? tabTitle;
    private string? tabPath;
    private string addressText = string.Empty;
    private bool isAddressBarEditing;
    private FileSystemObject? currentFileSystemObject;
    private double progressPercent;
    private bool isLoading;
    private string searchText = string.Empty;
    private bool isPreviewPaneVisible;
    private ViewMode currentViewMode = ViewMode.Details;

    public TabContentViewModel(ExplorerServices services)
    {
        this.services = services;
        policy = services.Policy;
        dialogs = services.Dialogs;

        watcher = new FolderWatcher(Reload);
        sidebar = new SidebarViewModel(policy);
        preview = new PreviewViewModel(policy);
        operations = new FileOperationsViewModel(this, services);
        itemsView = (ListCollectionView)CollectionViewSource.GetDefaultView(FileSystemObjects);

        OpenCmd = new ActionCommand(p => Open(p?.ToString()));
        BackCmd = new ActionCommand(_ => GoUp(), _ => TabPath != null);
        BackHome = new ActionCommand(_ => GoHome());
        NavigateBreadcrumbCmd = new ActionCommand(p => Open(p as string));
        RefreshCmd = new ActionCommand(_ => Reload());
        ToggleAddressBarEditCmd = new ActionCommand(_ => BeginAddressEdit());
        CommitAddressCmd = new ActionCommand(_ => CommitAddress());
        CancelAddressCmd = new ActionCommand(_ => IsAddressBarEditing = false);
        ClearSearchCmd = new ActionCommand(_ => SearchText = string.Empty);
        TogglePreviewPaneCmd = new ActionCommand(_ => IsPreviewPaneVisible = !IsPreviewPaneVisible);
        ToggleViewModeCmd = new ActionCommand(_ => CurrentViewMode = IsDetailsView ? ViewMode.Tiles : ViewMode.Details);
        OpenAboutCmd = new ActionCommand(_ => dialogs.ShowAbout());
        OpenAdminCmd = new ActionCommand(_ => OpenAdmin());

        services.Devices.DeviceChanged += OnDeviceChanged;
        services.Devices.DriveReleasing += OnDriveReleasing;
        services.Devices.DriveReleaseFinished += OnDriveReleaseFinished;
        services.Session.SessionReset += OnSessionReset;

        ApplySorting();
        UpdateBreadcrumbs();
    }

    public ICommand OpenCmd { get; }
    public ICommand BackCmd { get; }
    public ICommand BackHome { get; }
    public ICommand NavigateBreadcrumbCmd { get; }
    public ICommand RefreshCmd { get; }
    public ICommand ToggleAddressBarEditCmd { get; }
    public ICommand CommitAddressCmd { get; }
    public ICommand CancelAddressCmd { get; }
    public ICommand ClearSearchCmd { get; }
    public ICommand TogglePreviewPaneCmd { get; }
    public ICommand ToggleViewModeCmd { get; }
    public ICommand OpenAboutCmd { get; }
    public ICommand OpenAdminCmd { get; }

    public ICommand CopyCmd => operations.CopyCmd;
    public ICommand CutCmd => operations.CutCmd;
    public ICommand PasteCmd => operations.PasteCmd;
    public ICommand DeleteCmd => operations.DeleteCmd;
    public ICommand RenameCmd => operations.RenameCmd;
    public ICommand CreateDirectoryCmd => operations.CreateDirectoryCmd;
    public ICommand EjectDriveCmd => operations.EjectDriveCmd;
    public ICommand CalculateFolderSizeCmd => operations.CalculateFolderSizeCmd;

    public ObservableCollection<FileSystemObject> FileSystemObjects { get; } = new();
    public ObservableCollection<BreadcrumbItem> Breadcrumbs { get; } = new();
    public ObservableCollection<FileSystemObject> SidebarQuickAccess => sidebar.QuickAccess;
    public ObservableCollection<FileSystemObject> SidebarDrives => sidebar.Drives;
    public FilePreviewInfo PreviewInfo => preview.Info;
    public IReadOnlyList<FileSystemObject> SelectedFileSystemObjects => selectedItems;

    public SortColumn CurrentSortColumn { get; private set; } = SortColumn.Name;
    public ListSortDirection CurrentSortDirection { get; private set; } = ListSortDirection.Ascending;

    public string? TabTitle
    {
        get => tabTitle;
        private set => SetValue(ref tabTitle, value);
    }

    public string? TabPath
    {
        get => tabPath;
        internal set
        {
            if (SetValue(ref tabPath, value))
            {
                UpdateBreadcrumbs();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>What is being typed; the tab stays where it is until the text is committed.</summary>
    public string AddressText
    {
        get => addressText;
        set => SetValue(ref addressText, value);
    }

    public bool IsAddressBarEditing
    {
        get => isAddressBarEditing;
        set => SetValue(ref isAddressBarEditing, value);
    }

    public FileSystemObject? CurrentFileSystemObject
    {
        get => currentFileSystemObject;
        set
        {
            if (SetValue(ref currentFileSystemObject, value))
            {
                CommandManager.InvalidateRequerySuggested();
                UpdatePreview();
            }
        }
    }

    public double ProgressPercent
    {
        get => progressPercent;
        private set => SetValue(ref progressPercent, value);
    }

    public bool IsLoading
    {
        get => isLoading;
        set => SetValue(ref isLoading, value);
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetValue(ref searchText, value))
                ApplyFilter();
        }
    }

    public bool IsPreviewPaneVisible
    {
        get => isPreviewPaneVisible;
        set
        {
            if (SetValue(ref isPreviewPaneVisible, value))
                UpdatePreview();
        }
    }

    public ViewMode CurrentViewMode
    {
        get => currentViewMode;
        set
        {
            if (SetValue(ref currentViewMode, value))
            {
                OnPropertyChanged(nameof(IsDetailsView));
                OnPropertyChanged(nameof(IsTilesView));
            }
        }
    }

    public bool IsDetailsView => CurrentViewMode == ViewMode.Details;
    public bool IsTilesView => CurrentViewMode == ViewMode.Tiles;

    /// <summary>Shows the home screen. Separate from construction so that creating a tab reads no drives.</summary>
    public void Start()
    {
        RefreshSidebar();
        Reload();
    }

    public void Dispose()
    {
        disposed = true;
        services.Devices.DeviceChanged -= OnDeviceChanged;
        services.Devices.DriveReleasing -= OnDriveReleasing;
        services.Devices.DriveReleaseFinished -= OnDriveReleaseFinished;
        services.Session.SessionReset -= OnSessionReset;
        watcher.Dispose();
        preview.Dispose();
        scan.Cancel();
        scan.Dispose();
    }

    public void SetSelectedItems(IEnumerable<FileSystemObject> items)
    {
        selectedItems.Clear();
        selectedItems.AddRange(items);
        CommandManager.InvalidateRequerySuggested();
    }

    public void SortByColumn(string columnTag)
    {
        if (!Enum.TryParse<SortColumn>(columnTag, true, out var column))
            return;

        CurrentSortDirection = CurrentSortColumn == column && CurrentSortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
        CurrentSortColumn = column;

        ApplySorting();
        OnPropertyChanged(nameof(CurrentSortColumn));
        OnPropertyChanged(nameof(CurrentSortDirection));
    }

    /// <summary>Opens a folder or a file; an empty path is the home screen.</summary>
    public bool Open(string? rawPath)
    {
        string path = rawPath?.Trim().Trim('"') ?? string.Empty;
        if (path.Length == 0)
        {
            GoHome();
            return true;
        }

        if (Directory.Exists(path))
        {
            if (!policy.CanNavigate(path, out var reason))
                return Deny(path, reason);

            TabPath = Path.GetFullPath(path);
            Reload();
            return true;
        }

        if (File.Exists(path))
            return OpenFile(path);

        dialogs.Error($"הנתיב המבוקש אינו קיים או שאינו נגיש:\n{path}", "שגיאה");
        return false;
    }

    public void GoHome()
    {
        TabPath = null;
        Reload();
    }

    /// <summary>Loads the current path. Loading the same path again keeps the selection and the scroll position.</summary>
    public async void Reload()
    {
        if (disposed) return;

        scan.Cancel();
        scan.Dispose();
        scan = new CancellationTokenSource();
        var token = scan.Token;

        if (TabPath != null && !policy.CanNavigate(TabPath, out var reason))
        {
            string denied = TabPath;
            TabPath = null;
            Deny(denied, reason);
        }

        string? path = TabPath;
        bool isRefresh = hasLoaded && string.Equals(path, loadedPath, StringComparison.OrdinalIgnoreCase);

        UpdateTitle();
        watcher.Start(path);
        ProgressPercent = 0;
        IsLoading = true;

        if (!isRefresh)
        {
            hasLoaded = false;
            selectedItems.Clear();
            FileSystemObjects.Clear();
        }

        string? failure = null;
        try
        {
            await LoadAsync(path, isRefresh, token);
            loadedPath = path;
            hasLoaded = true;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (UnauthorizedAccessException ex)
        {
            failure = $"הגישה נדחתה:\n{ex.Message}";
            CrashLogger.LogMessage($"Access denied to {path}: {ex.Message}", "WARN");
        }
        catch (IOException ex)
        {
            failure = $"הכונן או התיקייה אינם זמינים (יתכן שההתקן נותק):\n{ex.Message}";
            CrashLogger.LogMessage($"{path} is unavailable: {ex.Message}", "WARN");
        }

        // A newer load owns the tab now.
        if (token.IsCancellationRequested)
            return;

        IsLoading = false;
        ProgressPercent = 100;
        CommandManager.InvalidateRequerySuggested();

        if (failure == null)
            return;

        // Leave the failed folder first: a message box lets other events in.
        if (path != null)
            GoHome();
        dialogs.Warn(failure, "שגיאה בגישה");
    }

    private async Task LoadAsync(string? path, bool isRefresh, CancellationToken token)
    {
        var refreshed = new List<FileSystemObject>();

        await foreach (var batch in services.Loader.LoadAsync(path, token))
        {
            token.ThrowIfCancellationRequested();
            ProgressPercent = batch.ProgressPercent;

            if (isRefresh)
            {
                refreshed.AddRange(batch.Items);
                continue;
            }

            foreach (var item in batch.Items)
                FileSystemObjects.Add(item);
        }

        token.ThrowIfCancellationRequested();
        if (isRefresh)
            Merge(refreshed);
    }

    private void Merge(List<FileSystemObject> latest)
    {
        var byPath = latest.ToDictionary(item => item.Path, StringComparer.OrdinalIgnoreCase);
        bool changed = false;
        bool currentChanged = false;

        foreach (var existing in FileSystemObjects.ToList())
        {
            if (!byPath.Remove(existing.Path, out var updated) || updated.IsDirectory != existing.IsDirectory)
            {
                FileSystemObjects.Remove(existing);
                if (updated != null)
                    byPath[updated.Path] = updated;
            }
            else if (existing.UpdateFrom(updated))
            {
                changed = true;
                currentChanged |= existing == CurrentFileSystemObject;
            }
        }

        foreach (var added in latest.Where(item => byPath.ContainsKey(item.Path)))
            FileSystemObjects.Add(added);

        if (changed)
            itemsView.Refresh();
        if (currentChanged)
            UpdatePreview();
    }

    private bool OpenFile(string path)
    {
        if (!policy.CanOpenFile(path, out var reason))
            return Deny(path, reason);

        string fullPath = Path.GetFullPath(path);
        try
        {
            Process.Start(new ProcessStartInfo(fullPath) { UseShellExecute = true })?.Dispose();
            services.Audit.Log(AuditEventType.FileOpened, fullPath, "File opened in default application");
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or IOException)
        {
            CrashLogger.LogException(ex, $"Failed to open file: {fullPath}");
            services.Audit.Log(AuditEventType.FileOpened, fullPath, ex.Message, false);
            dialogs.Error(ex.Message, "שגיאה בפתיחת הקובץ");
            return false;
        }
    }

    private bool Deny(string path, string reason)
    {
        services.Audit.Log(AuditEventType.SecurityBlocked, path, reason, false);
        dialogs.Warn(reason, "גישה נדחתה");
        return false;
    }

    private void GoUp()
    {
        if (TabPath == null) return;

        string? parent = Path.GetDirectoryName(TabPath);
        TabPath = !string.IsNullOrEmpty(parent) && policy.CanNavigate(parent, out _) ? parent : null;
        Reload();
    }

    private void BeginAddressEdit()
    {
        AddressText = TabPath ?? string.Empty;
        IsAddressBarEditing = true;
    }

    private void CommitAddress()
    {
        if (Open(AddressText))
            IsAddressBarEditing = false;
    }

    private void OpenAdmin()
    {
        if (!dialogs.AuthenticateAdmin() || !dialogs.ShowAdminSettings())
            return;

        CommandManager.InvalidateRequerySuggested();
        RefreshSidebar();
        Reload();
    }

    private void UpdateTitle()
    {
        if (string.IsNullOrEmpty(TabPath))
            TabTitle = "בית";
        else if (Path.GetPathRoot(TabPath) == TabPath)
            TabTitle = DriveDescriber.GetLabel(TabPath);
        else
            TabTitle = Path.GetFileName(TabPath);
    }

    private void UpdateBreadcrumbs()
    {
        Breadcrumbs.Clear();
        foreach (var item in BreadcrumbBuilder.Build(TabPath))
            Breadcrumbs.Add(item);
    }

    private void UpdatePreview() => _ = preview.LoadAsync(IsPreviewPaneVisible ? CurrentFileSystemObject : null);

    private void RefreshSidebar() => _ = sidebar.RefreshAsync();

    private void ApplySorting() => itemsView.CustomSort = new FileSystemObjectComparer(CurrentSortColumn, CurrentSortDirection);

    private void ApplyFilter()
    {
        string query = SearchText.Trim();
        itemsView.Filter = query.Length == 0
            ? null
            : item => item is FileSystemObject entry && entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsOnDrive(string root) =>
        TabPath != null && string.Equals(Path.GetPathRoot(TabPath), root, StringComparison.OrdinalIgnoreCase);

    private void OnDriveReleasing(object? sender, string root)
    {
        if (!IsOnDrive(root)) return;

        scan.Cancel();
        watcher.Stop();
        preview.Clear();
    }

    private void OnDriveReleaseFinished(object? sender, string root)
    {
        // Still mounted means the eject failed, so resume watching the folder.
        if (IsOnDrive(root) && Directory.Exists(TabPath))
            Reload();
    }

    private void OnSessionReset(object? sender, EventArgs e) => dispatcher.InvokeAsync(() =>
    {
        SearchText = string.Empty;
        preview.Clear();
        RefreshSidebar();
        GoHome();
    });

    private void OnDeviceChanged(object? sender, EventArgs e) => dispatcher.InvokeAsync(() =>
    {
        if (disposed) return;

        RefreshSidebar();
        if (TabPath == null)
        {
            Reload();
        }
        else if (!Directory.Exists(Path.GetPathRoot(TabPath)))
        {
            GoHome();
            dialogs.Info("הכונן נותק מהמחשב. חוזר לתצוגת הבית.", "התקן נותק");
        }
    });
}
