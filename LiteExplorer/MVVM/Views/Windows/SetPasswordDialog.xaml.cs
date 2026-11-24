using LiteExplorer.Services;
using System.Windows;

namespace LiteExplorer.MVVM.Views.Windows;

/// <summary>
/// First-time setup, or repair of a damaged configuration. Saving requires a Windows administrator.
/// </summary>
public partial class SetPasswordDialog : DialogWindow
{
    private readonly IConfigurationService configService;

    public SetPasswordDialog(IConfigurationService configService)
    {
        this.configService = configService;
        InitializeComponent();
        TxtMessage.Text = configService.IsLocked
            ? "קובץ ההגדרות פגום או אינו מהימן, והגישה נעולה. קביעת סיסמה תאפס את ההגדרות. נדרש אישור של מנהל Windows."
            : "עדיין לא נקבעה סיסמת מנהל. בשמירה יידרש אישור של מנהל Windows.";
        Loaded += (_, _) => TxtPassword.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (TxtPassword.Password.Length < ConfigurationService.MinimumPasswordLength)
        {
            TxtError.Text = $"הסיסמה חייבת להכיל לפחות {ConfigurationService.MinimumPasswordLength} תווים.";
            return;
        }

        if (TxtPassword.Password != TxtConfirm.Password)
        {
            TxtError.Text = "הסיסמאות אינן תואמות.";
            return;
        }

        var configuration = configService.CreateEditableCopy();
        configService.SetPassword(configuration, TxtPassword.Password);

        if (configService.Save(configuration, out var error))
            DialogResult = true;
        else
            TxtError.Text = error;
    }
}
