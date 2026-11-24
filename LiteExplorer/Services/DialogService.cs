using LiteExplorer.Infrastructure.Converters;
using LiteExplorer.MVVM.Models;
using LiteExplorer.MVVM.Views.Windows;
using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace LiteExplorer.Services;

public interface IDialogService
{
    void Info(string message, string title);
    void Warn(string message, string title);
    void Error(string message, string title);

    /// <summary>Asks for confirmation; cancelling is the default.</summary>
    bool Confirm(string message, string title, string acceptText);

    string? AskText(string prompt, string title, string initialValue);
    bool AskNewFolder([NotNullWhen(true)] out string? name);
    bool ConfirmLowSpace(long neededBytes, long freeBytes);
    ConflictResolutionResult AskConflict(string fileName, string existingPath, string incomingPath);
    bool AuthenticateAdmin();
    bool ShowAdminSettings();
    void ShowAbout();
}

public class DialogService : IDialogService
{
    private readonly IPathPolicy policy;
    private readonly IConfigurationService configuration;

    public DialogService(IPathPolicy policy, IConfigurationService configuration)
    {
        this.policy = policy;
        this.configuration = configuration;
    }

    private static Window? Owner => Application.Current?.MainWindow;

    public void Info(string message, string title) => MessageDialog.Show(Owner, message, title, MessageKind.Info);

    public void Warn(string message, string title) => MessageDialog.Show(Owner, message, title, MessageKind.Warning);

    public void Error(string message, string title) => MessageDialog.Show(Owner, message, title, MessageKind.Error);

    public bool Confirm(string message, string title, string acceptText) =>
        MessageDialog.Show(Owner, message, title, MessageKind.Question, acceptText);

    public string? AskText(string prompt, string title, string initialValue) =>
        TextInputDialog.Prompt(Owner, prompt, title, initialValue);

    public bool AskNewFolder([NotNullWhen(true)] out string? name) => NewFolderDialog.Prompt(Owner, out name);

    /// <summary>Called from the transfer thread.</summary>
    public bool ConfirmLowSpace(long neededBytes, long freeBytes) => Application.Current.Dispatcher.Invoke(() => Confirm(
        $"נראה שאין די מקום ביעד.\nדרוש: \u200E{BytesToNormalSize.FormatBytes(neededBytes)}\u200E, פנוי: \u200E{BytesToNormalSize.FormatBytes(freeBytes)}\u200E.\n\nלהתחיל בכל זאת?",
        "אין די מקום", "המשך"));

    /// <summary>Called from the transfer thread.</summary>
    public ConflictResolutionResult AskConflict(string fileName, string existingPath, string incomingPath) =>
        Application.Current.Dispatcher.Invoke(() => ConflictResolutionDialog.Prompt(Owner, fileName, existingPath, incomingPath));

    public bool AuthenticateAdmin() => AdminPasswordDialog.Prompt(configuration, Owner);

    public bool ShowAdminSettings()
    {
        configuration.ApplyToPolicy(policy);
        return new AdminSettingsWindow(configuration, policy) { Owner = Owner }.ShowDialog() == true;
    }

    public void ShowAbout() => new AboutWindow { Owner = Owner }.ShowDialog();
}
