using LiteExplorer.Infrastructure.Converters;
using LiteExplorer.MVVM.Models;
using System;
using System.IO;
using System.Windows;

namespace LiteExplorer.MVVM.Views.Windows;

public partial class ConflictResolutionDialog : DialogWindow
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
        TxtExistingInfo.Text = Describe(existingPath);
        TxtIncomingInfo.Text = Describe(incomingPath);
    }

    private static string Describe(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists
                ? $"גודל: \u200E{BytesToNormalSize.FormatBytes(info.Length)}\u200E, עודכן: \u200E{info.LastWriteTime:dd/MM/yyyy HH:mm}\u200E"
                : "הקובץ אינו זמין לקריאה.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "שגיאה בקריאת פרטי הקובץ.";
        }
    }

    private void OnReplaceClick(object sender, RoutedEventArgs e) => Choose(ConflictResolutionAction.Replace);

    private void OnSkipClick(object sender, RoutedEventArgs e) => Choose(ConflictResolutionAction.Skip);

    private void OnKeepBothClick(object sender, RoutedEventArgs e) => Choose(ConflictResolutionAction.KeepBoth);

    private void Choose(ConflictResolutionAction action)
    {
        Result.Action = action;
        Result.ApplyToAll = ChkApplyToAll.IsChecked == true;
        DialogResult = true;
    }

    public static ConflictResolutionResult Prompt(Window? owner, string fileName, string existingPath, string incomingPath)
    {
        var dlg = new ConflictResolutionDialog(fileName, existingPath, incomingPath);
        if (owner != null)
        {
            dlg.Owner = owner;
        }

        // Closing the window in any other way cancels the transfer.
        if (dlg.ShowDialog() != true)
            dlg.Result.Action = ConflictResolutionAction.Cancel;
        return dlg.Result;
    }
}
