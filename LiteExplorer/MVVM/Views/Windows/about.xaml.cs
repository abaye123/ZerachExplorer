using System.Windows;

namespace LiteExplorer.MVVM.Views.Windows;

public partial class about : Window
{
    public about() => InitializeComponent();

    private void Button_Click(object sender, RoutedEventArgs e) => Close();
}
