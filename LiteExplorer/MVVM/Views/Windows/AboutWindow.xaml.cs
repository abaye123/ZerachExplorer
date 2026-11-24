using System.Windows;

namespace LiteExplorer.MVVM.Views.Windows;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        LblVersion.Content = $"זרח אקספלורר v{typeof(AboutWindow).Assembly.GetName().Version?.ToString(3)}";
    }

    private void Button_Click(object sender, RoutedEventArgs e) => Close();
}
