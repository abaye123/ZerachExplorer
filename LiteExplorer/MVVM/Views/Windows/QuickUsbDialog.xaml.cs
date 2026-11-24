using System.Windows;

namespace LiteExplorer.MVVM.Views.Windows;

public partial class QuickUsbDialog : DialogWindow
{
    public QuickUsbDialog(string driveLabel)
    {
        InitializeComponent();
        TxtDriveInfo.Text = $"{driveLabel}\nלפתוח את תוכן הכונן?";
    }

    private void OpenDrive_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
