using LiteExplorer.Services;
using System;
using System.IO;
using System.Windows;

namespace LiteExplorer.MVVM.Views.Windows;

public partial class NewFolderDialog : Window
{
    private readonly string parentDirectory;
    private readonly IPathPolicy pathPolicy;

    public string FolderName { get; private set; }

    public NewFolderDialog(string parentDirectory, IPathPolicy pathPolicy)
    {
        InitializeComponent();
        this.parentDirectory = parentDirectory;
        this.pathPolicy = pathPolicy ?? new PathPolicy();

        Loaded += (_, _) =>
        {
            TxtFolderName.Focus();
            TxtFolderName.SelectAll();
        };
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        string name = TxtFolderName.Text?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError("נא להזין שם תיקייה.");
            return;
        }

        if (!pathPolicy.CanCreateDirectory(parentDirectory, name, out string reason))
        {
            ShowError(reason ?? "יצירת התיקייה אינה מורשית בנתיב זה.");
            return;
        }

        string fullPath = Path.Combine(parentDirectory, name);
        if (Directory.Exists(fullPath))
        {
            ShowError("תיקייה בשם זה כבר קיימת בנתיב היעד.");
            return;
        }

        FolderName = name;
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string message)
    {
        TxtError.Text = message;
        TxtError.Visibility = Visibility.Visible;
    }

    public static bool Prompt(Window owner, string parentDirectory, IPathPolicy pathPolicy, out string createdFolderName)
    {
        createdFolderName = null;
        var dlg = new NewFolderDialog(parentDirectory, pathPolicy);
        if (owner != null)
        {
            dlg.Owner = owner;
        }

        if (dlg.ShowDialog() == true)
        {
            createdFolderName = dlg.FolderName;
            return true;
        }

        return false;
    }
}
