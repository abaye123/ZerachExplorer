using LiteExplorer.MVVM.Models;
using LiteExplorer.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace LiteExplorer.MVVM.Views.Windows;

public partial class AdminSettingsWindow : Window
{
    private readonly IConfigurationService configService;
    private readonly IPathPolicy pathPolicy;
    private readonly AppConfiguration workingConfig;

    private readonly ObservableCollection<string> extensions = new();
    private readonly ObservableCollection<string> directories = new();

    public AdminSettingsWindow(IConfigurationService configService, IPathPolicy pathPolicy)
    {
        this.configService = configService;
        this.pathPolicy = pathPolicy;
        this.workingConfig = configService.Configuration;

        InitializeComponent();

        LstExtensions.ItemsSource = extensions;
        LstDirectories.ItemsSource = directories;

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
    }

    private void CmbProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbProfile.SelectedItem is not ComboBoxItem item || item.Tag == null) return;
        string tag = item.Tag.ToString();

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
        string text = TxtNewExtension.Text?.Trim().ToLowerInvariant();
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
        string text = TxtNewDirectory.Text?.Trim();
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
            MessageBox.Show($"נתיב לא חוקי: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        if (string.IsNullOrWhiteSpace(newPass))
        {
            TxtPasswordStatus.Text = "הסיסמה אינה יכולה להיות ריקה.";
            TxtPasswordStatus.Foreground = System.Windows.Media.Brushes.Red;
            TxtPasswordStatus.Visibility = Visibility.Visible;
            return;
        }

        if (newPass != confirmPass)
        {
            TxtPasswordStatus.Text = "הסיסמאות אינן תואמות.";
            TxtPasswordStatus.Foreground = System.Windows.Media.Brushes.Red;
            TxtPasswordStatus.Visibility = Visibility.Visible;
            return;
        }

        configService.ChangePassword(newPass);
        TxtPasswordStatus.Text = "הסיסמה עודכנה בהצלחה (תשמר בלחיצה על שמור).";
        TxtPasswordStatus.Foreground = System.Windows.Media.Brushes.Green;
        TxtPasswordStatus.Visibility = Visibility.Visible;
        TxtNewPassword.Clear();
        TxtConfirmPassword.Clear();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
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

        if (configService.Save(workingConfig, out string error))
        {
            configService.ApplyToPolicy(pathPolicy);
            DialogResult = true;
            Close();
        }
        else
        {
            TxtStatusMessage.Text = error;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
