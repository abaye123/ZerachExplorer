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

    /// <summary>Asks a yes/no question where "no" is the default.</summary>
    bool Confirm(string message, string title);

    string? AskText(string prompt, string title, string initialValue);
    bool AskNewFolder(string parentDirectory, [NotNullWhen(true)] out string? name);
    ConflictResolutionResult AskConflict(string fileName, string existingPath, string incomingPath);
    bool AuthenticateAdmin();
    bool ShowAdminSettings();
    void ShowAbout();
}

public class DialogService : IDialogService
{
    private const MessageBoxOptions Rtl = MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading;

    private readonly IPathPolicy policy;
    private readonly IConfigurationService configuration;

    public DialogService(IPathPolicy policy, IConfigurationService configuration)
    {
        this.policy = policy;
        this.configuration = configuration;
    }

    private static Window? Owner => Application.Current?.MainWindow;

    public void Info(string message, string title) => Show(message, title, MessageBoxImage.Information);

    public void Warn(string message, string title) => Show(message, title, MessageBoxImage.Warning);

    public void Error(string message, string title) => Show(message, title, MessageBoxImage.Error);

    public bool Confirm(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No, Rtl) == MessageBoxResult.Yes;

    public string? AskText(string prompt, string title, string initialValue)
    {
        string answer = Microsoft.VisualBasic.Interaction.InputBox(prompt, title, initialValue);
        return string.IsNullOrWhiteSpace(answer) ? null : answer.Trim();
    }

    public bool AskNewFolder(string parentDirectory, [NotNullWhen(true)] out string? name) =>
        NewFolderDialog.Prompt(Owner, parentDirectory, policy, out name);

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

    private static void Show(string message, string title, MessageBoxImage image) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, image, MessageBoxResult.OK, Rtl);
}
