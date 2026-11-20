using LiteExplorer.MVVM.Models;
using LiteExplorer.Services;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;

namespace LiteExplorer.MVVM.Views.Windows;

public partial class MainWindow
{
    private const int WM_DEVICECHANGE = 0x0219;
    private const int DBT_DEVICEARRIVAL = 0x8000;
    private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnWindowLoaded;
        Closing += OnWindowClosing;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        KioskController.Instance.ApplyKioskWindowSettings(this);
    }

    private void OnWindowClosing(object sender, CancelEventArgs e)
    {
        KioskController.Instance.HandleWindowClosing(this, e, () => AdminPasswordDialog.Prompt(this));
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WndProc);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_DEVICECHANGE)
        {
            int wp = wParam.ToInt32();
            if (wp == DBT_DEVICEARRIVAL)
            {
                AuditService.Instance.Log(AuditEventType.UsbConnected, "USB", "Removable device arrival detected", true);
                DeviceNotificationService.Instance.NotifyDeviceChanged();
            }
            else if (wp == DBT_DEVICEREMOVECOMPLETE)
            {
                AuditService.Instance.Log(AuditEventType.UsbDisconnected, "USB", "Removable device removal detected", true);
                SessionService.Instance.CleanTempFiles();
                DeviceNotificationService.Instance.NotifyDeviceChanged();
            }
        }
        return IntPtr.Zero;
    }
}
