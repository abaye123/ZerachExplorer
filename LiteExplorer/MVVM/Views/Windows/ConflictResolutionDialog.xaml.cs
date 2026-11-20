using LiteExplorer.Infrastructure.Converters;
using LiteExplorer.MVVM.Models;
using System;
using System.IO;
using System.Windows;

namespace LiteExplorer.MVVM.Views.Windows;

public partial class ConflictResolutionDialog : Window
{
    public ConflictResolutionResult Result { get; } = new();

    public ConflictResolutionDialog(string fileName, string existingPath, string incomingPath)
    {
        InitializeComponent();

        TxtFileName.Text = fileName;
        PopulateFileInfo(existingPath, incomingPath);
    }

    private void PopulateFileInfo(string existingPath, string incomingPath)
    {
        var sizeConverter = new BytesToNormalSize();

        try
        {
            if (File.Exists(existingPath))
            {
                var fi = new FileInfo(existingPath);
                string sizeStr = sizeConverter.Convert(fi.Length, typeof(string), null, null)?.ToString() ?? $"{fi.Length} Bytes";
                TxtExistingInfo.Text = $"גודל: {sizeStr} | עודכן: {fi.LastWriteTime:dd/MM/yyyy HH:mm}";
            }
            else
            {
                TxtExistingInfo.Text = "הקובץ אינו זמין לקריאה.";
            }
        }
        catch
        {
            TxtExistingInfo.Text = "שגיאה בקריאת פרטי הקובץ.";
        }

        try
        {
            if (File.Exists(incomingPath))
            {
                var fi = new FileInfo(incomingPath);
                string sizeStr = sizeConverter.Convert(fi.Length, typeof(string), null, null)?.ToString() ?? $"{fi.Length} Bytes";
                TxtIncomingInfo.Text = $"גודל: {sizeStr} | עודכן: {fi.LastWriteTime:dd/MM/yyyy HH:mm}";
            }
            else
            {
                TxtIncomingInfo.Text = "הקובץ אינו זמין לקריאה.";
            }
        }
        catch
        {
            TxtIncomingInfo.Text = "שגיאה בקריאת פרטי הקובץ.";
        }
    }

    private void OnReplaceClick(object sender, RoutedEventArgs e)
    {
        Result.Action = ConflictResolutionAction.Replace;
        Result.ApplyToAll = ChkApplyToAll.IsChecked == true;
        DialogResult = true;
        Close();
    }

    private void OnSkipClick(object sender, RoutedEventArgs e)
    {
        Result.Action = ConflictResolutionAction.Skip;
        Result.ApplyToAll = ChkApplyToAll.IsChecked == true;
        DialogResult = true;
        Close();
    }

    private void OnKeepBothClick(object sender, RoutedEventArgs e)
    {
        Result.Action = ConflictResolutionAction.KeepBoth;
        Result.ApplyToAll = ChkApplyToAll.IsChecked == true;
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Result.Action = ConflictResolutionAction.Cancel;
        Result.ApplyToAll = false;
        DialogResult = false;
        Close();
    }

    public static ConflictResolutionResult Prompt(Window owner, string fileName, string existingPath, string incomingPath)
    {
        var dlg = new ConflictResolutionDialog(fileName, existingPath, incomingPath);
        if (owner != null)
        {
            dlg.Owner = owner;
        }

        dlg.ShowDialog();
        return dlg.Result;
    }
}
