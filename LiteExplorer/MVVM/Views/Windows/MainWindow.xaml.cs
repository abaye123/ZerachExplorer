using LiteExplorer.Services;
using System;
using System.Windows;
using System.Windows.Interop;

namespace LiteExplorer.MVVM.Views.Windows;

public partial class MainWindow
{
    private const int WM_DEVICECHANGE = 0x0219;
    private const int DBT_DEVICEARRIVAL = 0x8000;
    private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;

    public MainWindow() => InitializeComponent();

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
            if (wp == DBT_DEVICEARRIVAL || wp == DBT_DEVICEREMOVECOMPLETE)
            {
                DeviceNotificationService.Instance.NotifyDeviceChanged();
            }
        }
        return IntPtr.Zero;
    }
}
