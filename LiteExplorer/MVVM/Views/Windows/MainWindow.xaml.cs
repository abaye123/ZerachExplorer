using LiteExplorer.Helpers.WinAPI;
using LiteExplorer.MVVM.Models;
using LiteExplorer.MVVM.ViewModels;
using LiteExplorer.Services;
using System.ComponentModel;
using System.IO;
using System.Windows;
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

        var dialog = new QuickUsbDialog(root, DriveDescriber.GetLabel(drive)) { Owner = this };
        if (dialog.ShowDialog() == true)
            ((MainWindowViewModel)DataContext).OpenInAvailableTab(root);
    }
}
