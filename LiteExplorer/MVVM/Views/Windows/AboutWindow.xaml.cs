using System.Windows;

namespace LiteExplorer.MVVM.Views.Windows;

public partial class AboutWindow : DialogWindow
{
    public AboutWindow()
    {
        InitializeComponent();
        LblVersion.Text = $"גרסה {typeof(AboutWindow).Assembly.GetName().Version?.ToString(3)}";
    }

    private void Button_Click(object sender, RoutedEventArgs e) => Close();
}
