using LiteExplorer.Helpers.WinAPI;
using LiteExplorer.MVVM.Models;
using LiteExplorer.MVVM.ViewModels;
using LiteExplorer.Services;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace LiteExplorer.MVVM.Views.Windows;

public partial class MainWindow
{
    private readonly IConfigurationService configuration;
    private readonly IPathPolicy policy;

    public MainWindow(IConfigurationService configuration, IPathPolicy policy)
    {
        this.configuration = configuration;
        this.policy = policy;
        InitializeComponent();
        Loaded += (_, _) => KioskController.Instance.ApplyKioskWindowSettings(this);
        Closing += OnWindowClosing;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Tab_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle) return;

        ViewModel.CloseTabCmd.Execute(((FrameworkElement)sender).DataContext);
        e.Handled = true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 || (Keyboard.Modifiers & ModifierKeys.Alt) != 0) return;

        if (e.Key == Key.Tab)
            ViewModel.CycleTab((Keyboard.Modifiers & ModifierKeys.Shift) == 0 ? 1 : -1);
        else if (e.Key is >= Key.D1 and <= Key.D8 && Keyboard.Modifiers == ModifierKeys.Control && e.Key - Key.D1 < ViewModel.Tabs.Count)
            ViewModel.SelectTab(e.Key - Key.D1);
        else if (e.Key == Key.D9 && Keyboard.Modifiers == ModifierKeys.Control)
            ViewModel.SelectTab(-1);
        else
            return;

        e.Handled = true;
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e) =>
        KioskController.Instance.HandleWindowClosing(this, e, () => AdminPasswordDialog.Prompt(configuration, this));

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (PresentationSource.FromVisual(this) is HwndSource source)
            source.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == DeviceMessage.Id && DeviceMessage.TryParse(wParam, lParam, out var arrived, out var roots))
        {
            // Opening a window inside the message would block the sender.
            Dispatcher.InvokeAsync(() => OnVolumesChanged(arrived, roots));
        }
        return IntPtr.Zero;
    }

    private void OnVolumesChanged(bool arrived, IReadOnlyList<string> roots)
    {
        foreach (var root in roots)
            AuditService.Instance.Log(arrived ? AuditEventType.UsbConnected : AuditEventType.UsbDisconnected, root);

        if (!arrived)
            SessionService.Instance.CleanTempFiles();

        DeviceNotificationService.Instance.NotifyDeviceChanged();

        if (arrived && configuration.Configuration.ShowUsbDialog)
        {
            foreach (var root in roots)
                OfferToOpen(root);
        }
    }

    private void OfferToOpen(string root)
    {
        var drive = new DriveInfo(root);
        if (drive.DriveType != DriveType.Removable || !policy.IsDriveAllowed(drive, out _))
            return;

        var dialog = new QuickUsbDialog(DriveDescriber.GetLabel(drive)) { Owner = this };
        if (dialog.ShowDialog() == true)
            ViewModel.OpenInAvailableTab(root);
    }
}
