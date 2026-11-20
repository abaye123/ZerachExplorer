using LiteExplorer.Services;
using System.Windows;
using System.Windows.Input;

namespace LiteExplorer.MVVM.Views.Windows;

public partial class AdminPasswordDialog : Window
{
    private readonly IConfigurationService configService;

    public bool IsAuthenticated { get; private set; }

    public static bool Prompt(Window owner = null)
    {
        var config = ConfigurationService.Instance;
        var dialog = new AdminPasswordDialog(config);
        if (owner != null) dialog.Owner = owner;
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
        if (configService.VerifyPassword(entered))
        {
            IsAuthenticated = true;
            DialogResult = true;
            Close();
        }
        else
        {
            TxtError.Text = "הסיסמה שהוזנה שגויה.";
            TxtError.Visibility = Visibility.Visible;
            TxtPassword.SelectAll();
            TxtPassword.Focus();
        }
    }
}
