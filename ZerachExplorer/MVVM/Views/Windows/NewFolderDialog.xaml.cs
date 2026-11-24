using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;

namespace ZerachExplorer.MVVM.Views.Windows;

/// <summary>Asks for a name. Whether the folder may be created is checked in the background, as it can be slow.</summary>
public partial class NewFolderDialog : DialogWindow
{
    public NewFolderDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            TxtFolderName.Focus();
            TxtFolderName.SelectAll();
        };
    }

    private string FolderName => TxtFolderName.Text.Trim();

    public static bool Prompt(Window? owner, [NotNullWhen(true)] out string? name)
    {
        var dialog = new NewFolderDialog { Owner = owner };
        name = dialog.ShowDialog() == true ? dialog.FolderName : null;
        return name != null;
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        string? error = FolderName.Length == 0 ? "נא להזין שם תיקייה."
            : FolderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || FolderName.Contains("..") ? "שם התיקייה מכיל תווים שאינם חוקיים."
            : null;

        if (error == null)
        {
            DialogResult = true;
            return;
        }

        TxtError.Text = error;
        TxtError.Visibility = Visibility.Visible;
    }
}
