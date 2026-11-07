using System.Windows;

namespace LiteExplorer.MVVM.Views.Windows;

public partial class QuickUsbDialog : Window
{
    public string DrivePath { get; }
    public bool OpenRequested { get; private set; }

    public QuickUsbDialog(string drivePath, string driveLabel)
    {
        InitializeComponent();
        DrivePath = drivePath;
        string label = string.IsNullOrWhiteSpace(driveLabel) ? "כונן נשלף" : driveLabel;
        TxtDriveInfo.Text = $"התחבר התקן: {label} ({drivePath}).\nהאם ברצונך לפתוח את תוכן ההתקן?";
    }

    private void OpenDrive_Click(object sender, RoutedEventArgs e)
    {
        OpenRequested = true;
        DialogResult = true;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        OpenRequested = false;
        DialogResult = false;
        Close();
    }
}
