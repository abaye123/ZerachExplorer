using ZerachExplorer.MVVM.Models;
using ZerachExplorer.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ZerachExplorer.MVVM.Views.Windows;

public partial class AdminSettingsWindow : DialogWindow
{
    private readonly IConfigurationService configService;
    private readonly IPathPolicy pathPolicy;
    private readonly AppConfiguration workingConfig;

    private readonly ObservableCollection<string> extensions = new();
    private readonly ObservableCollection<string> directories = new();

    private readonly ObservableCollection<DriveChoice> drives = new();

    public class DriveChoice
    {
        public string Root { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public bool IsBlocked { get; set; }
    }

    public AdminSettingsWindow(IConfigurationService configService, IPathPolicy pathPolicy)
    {
        this.configService = configService;
        this.pathPolicy = pathPolicy;
        this.workingConfig = configService.CreateEditableCopy();

        InitializeComponent();

        LstExtensions.ItemsSource = extensions;
        LstDirectories.ItemsSource = directories;
        LstDrives.ItemsSource = drives;

        LoadFormValues();
    }

    private void LoadFormValues()
    {
        switch (workingConfig.Profile)
        {
            case KioskProfile.ReadOnly:
                CmbProfile.SelectedIndex = 1;
                break;
            case KioskProfile.Controlled:
                CmbProfile.SelectedIndex = 2;
                break;
            case KioskProfile.Custom:
                CmbProfile.SelectedIndex = 3;
                break;
            default:
                CmbProfile.SelectedIndex = 0;
                break;
        }

        var p = workingConfig.Permissions ?? PathPermissions.CreateStandard();
        ChkKioskMode.IsChecked = workingConfig.KioskModeEnabled;
        ChkPreventExit.IsChecked = workingConfig.PreventAppExit;
        ChkUsbDialog.IsChecked = workingConfig.ShowUsbDialog;
        TxtLogDays.Text = workingConfig.LogRetentionDays.ToString();
        TxtLogSize.Text = workingConfig.LogMaxSizeMegabytes.ToString();
        ChkCanList.IsChecked = p.CanList;
        ChkCanOpen.IsChecked = p.CanOpen;
        ChkCanCopyFrom.IsChecked = p.CanCopyFrom;
        ChkCanCopyTo.IsChecked = p.CanCopyTo;
        ChkCanRename.IsChecked = p.CanRename;
        ChkCanDelete.IsChecked = p.CanDelete;
        ChkCanCreateDir.IsChecked = p.CanCreateDirectory;

        extensions.Clear();
        foreach (var ext in workingConfig.AllowedExtensions) extensions.Add(ext);

        directories.Clear();
        foreach (var dir in workingConfig.AllowedExplicitDirectories) directories.Add(dir);

        drives.Clear();
        var connected = DriveInfo.GetDrives().ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
        // Keep blocked drives listed even while they are disconnected.
        foreach (var root in connected.Keys.Union(workingConfig.BlockedDriveRoots, StringComparer.OrdinalIgnoreCase).OrderBy(r => r))
        {
            if (string.Equals(root, PathPolicy.SystemDriveRoot, StringComparison.OrdinalIgnoreCase))
                continue;

            drives.Add(new DriveChoice
            {
                Root = root,
                Label = connected.TryGetValue(root, out var drive) ? DescribeDrive(drive) : $"{root} (לא מחובר)",
                IsBlocked = workingConfig.BlockedDriveRoots.Contains(root, StringComparer.OrdinalIgnoreCase)
            });
        }
    }

    private static string DescribeDrive(DriveInfo drive)
    {
        try
        {
            return drive.IsReady ? DriveDescriber.GetLabel(drive) : drive.Name;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return drive.Name;
        }
    }

    private void CmbProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbProfile.SelectedItem is not ComboBoxItem item || item.Tag == null) return;
        string? tag = item.Tag.ToString();

        switch (tag)
        {
            case "ReadOnly":
                ApplyPermissionsToUI(PathPermissions.CreateReadOnly());
                break;
            case "Controlled":
                ApplyPermissionsToUI(PathPermissions.CreateControlled());
                break;
            case "Standard":
                ApplyPermissionsToUI(PathPermissions.CreateStandard());
                break;
        }
    }

    private void ApplyPermissionsToUI(PathPermissions p)
    {
        ChkCanList.IsChecked = p.CanList;
        ChkCanOpen.IsChecked = p.CanOpen;
        ChkCanCopyFrom.IsChecked = p.CanCopyFrom;
        ChkCanCopyTo.IsChecked = p.CanCopyTo;
        ChkCanRename.IsChecked = p.CanRename;
        ChkCanDelete.IsChecked = p.CanDelete;
        ChkCanCreateDir.IsChecked = p.CanCreateDirectory;
    }

    private void AddExtension_Click(object sender, RoutedEventArgs e)
    {
        string? text = TxtNewExtension.Text?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(text)) return;
        if (!text.StartsWith(".")) text = "." + text;

        if (!extensions.Contains(text))
        {
            extensions.Add(text);
            TxtNewExtension.Clear();
        }
    }

    private void RemoveExtension_Click(object sender, RoutedEventArgs e)
    {
        if (LstExtensions.SelectedItem is string selected)
        {
            extensions.Remove(selected);
        }
    }

    private void ResetExtensions_Click(object sender, RoutedEventArgs e)
    {
        string[] defaults = { ".doc", ".docx", ".xls", ".xlsx", ".pdf", ".txt", ".csv", ".one", ".onetoc2" };
        extensions.Clear();
        foreach (var ext in defaults) extensions.Add(ext);
    }

    private void AddDirectory_Click(object sender, RoutedEventArgs e)
    {
        string? text = TxtNewDirectory.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        try
        {
            string full = Path.GetFullPath(text);
            if (!directories.Contains(full))
            {
                directories.Add(full);
                TxtNewDirectory.Clear();
            }
        }
        catch (Exception ex)
        {
            MessageDialog.Show(this, $"נתיב לא חוקי: {ex.Message}", "תיקיות מאושרות", MessageKind.Warning);
        }
    }

    private void RemoveDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (LstDirectories.SelectedItem is string selected)
        {
            directories.Remove(selected);
        }
    }

    private void UpdatePassword_Click(object sender, RoutedEventArgs e)
    {
        string newPass = TxtNewPassword.Password ?? string.Empty;
        string confirmPass = TxtConfirmPassword.Password ?? string.Empty;

        if (newPass.Length < ConfigurationService.MinimumPasswordLength)
        {
            TxtPasswordStatus.Text = $"הסיסמה חייבת להכיל לפחות {ConfigurationService.MinimumPasswordLength} תווים.";
            TxtPasswordStatus.Foreground = (System.Windows.Media.Brush)FindResource("ErrorBrush");
            TxtPasswordStatus.Visibility = Visibility.Visible;
            return;
        }

        if (newPass != confirmPass)
        {
            TxtPasswordStatus.Text = "הסיסמאות אינן תואמות.";
            TxtPasswordStatus.Foreground = (System.Windows.Media.Brush)FindResource("ErrorBrush");
            TxtPasswordStatus.Visibility = Visibility.Visible;
            return;
        }

        configService.SetPassword(workingConfig, newPass);
        TxtPasswordStatus.Text = "הסיסמה עודכנה בהצלחה (תשמר בלחיצה על שמור).";
        TxtPasswordStatus.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
        TxtPasswordStatus.Visibility = Visibility.Visible;
        TxtNewPassword.Clear();
        TxtConfirmPassword.Clear();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadNumber(TxtLogDays, AppConfiguration.MaximumLogRetentionDays, out int logDays) ||
            !TryReadNumber(TxtLogSize, AppConfiguration.MaximumLogMaxSizeMegabytes, out int logSize))
        {
            TxtStatusMessage.Text = "ערכי היומן אינם תקינים: 1-365 ימים, 1-500 מגה-בייט.";
            return;
        }

        workingConfig.LogRetentionDays = logDays;
        workingConfig.LogMaxSizeMegabytes = logSize;
        workingConfig.ShowUsbDialog = ChkUsbDialog.IsChecked == true;

        var item = CmbProfile.SelectedItem as ComboBoxItem;
        string tag = item?.Tag?.ToString() ?? "Standard";

        workingConfig.Profile = tag switch
        {
            "ReadOnly" => KioskProfile.ReadOnly,
            "Controlled" => KioskProfile.Controlled,
            "Custom" => KioskProfile.Custom,
            _ => KioskProfile.Standard
        };

        workingConfig.Permissions = new PathPermissions
        {
            CanList = ChkCanList.IsChecked == true,
            CanOpen = ChkCanOpen.IsChecked == true,
            CanCopyFrom = ChkCanCopyFrom.IsChecked == true,
            CanCopyTo = ChkCanCopyTo.IsChecked == true,
            CanRename = ChkCanRename.IsChecked == true,
            CanDelete = ChkCanDelete.IsChecked == true,
            CanCreateDirectory = ChkCanCreateDir.IsChecked == true
        };

        workingConfig.AllowedExtensions = extensions.ToList();
        workingConfig.AllowedExplicitDirectories = directories.ToList();

        workingConfig.KioskModeEnabled = ChkKioskMode.IsChecked == true;
        workingConfig.PreventAppExit = ChkPreventExit.IsChecked == true;
        workingConfig.BlockedDriveRoots = drives.Where(d => d.IsBlocked).Select(d => d.Root).ToList();

        if (configService.Save(workingConfig, out string? error))
        {
            configService.ApplyToPolicy(pathPolicy);
            KioskController.Instance.Configure(configService.Configuration);
            LogLimits.Apply(configService.Configuration);
            KioskController.Instance.ApplyKioskWindowSettings(Application.Current.MainWindow);
            AuditService.Instance.Log(AuditEventType.AdminAccess, "Settings", "Settings saved", true);
            DialogResult = true;
        }
        else
        {
            TxtStatusMessage.Text = error;
        }
    }

    private static bool TryReadNumber(TextBox box, int maximum, out int value) =>
        int.TryParse(box.Text.Trim(), out value) && value >= 1 && value <= maximum;
}
