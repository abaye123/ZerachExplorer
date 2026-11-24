using LiteExplorer.MVVM.Models;
using System;
using LiteExplorer.Services;
using System.Windows;
using System.Windows.Input;

namespace LiteExplorer.MVVM.Views.Windows;

public partial class AdminPasswordDialog : Window
{
    private readonly IConfigurationService configService;

    public bool IsAuthenticated { get; private set; }

    /// <summary>
    /// Authenticates an administrator, or runs first-time setup when no password exists yet.
    /// </summary>
    public static bool Prompt(IConfigurationService configService, Window? owner)
    {
        if (!configService.HasPassword)
            return new SetPasswordDialog(configService) { Owner = owner }.ShowDialog() == true;

        var dialog = new AdminPasswordDialog(configService) { Owner = owner };
        return dialog.ShowDialog() == true && dialog.IsAuthenticated;
    }

    public AdminPasswordDialog(IConfigurationService configService)
    {
        this.configService = configService;
        InitializeComponent();
        Loaded += (_, _) => TxtPassword.Focus();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        ValidateAndConfirm();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ValidateAndConfirm();
        }
    }

    private void ValidateAndConfirm()
    {
        string entered = TxtPassword.Password ?? string.Empty;
        if (configService.VerifyPassword(entered, out var retryAfter))
        {
            IsAuthenticated = true;
            AuditService.Instance.Log(AuditEventType.AdminAccess, "Login", "Administrator authenticated", true);
            DialogResult = true;
            Close();
        }
        else
        {
            AuditService.Instance.Log(AuditEventType.AdminAccess, "Login", "Wrong password", false);
            TxtError.Text = retryAfter > TimeSpan.Zero
                ? $"יותר מדי ניסיונות. נסה שוב בעוד {Math.Ceiling(retryAfter.TotalSeconds)} שניות."
                : "הסיסמה שהוזנה שגויה.";
            TxtError.Visibility = Visibility.Visible;
            TxtPassword.SelectAll();
            TxtPassword.Focus();
        }
    }
}
