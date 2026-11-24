using LiteExplorer.MVVM.Models;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Shell;

namespace LiteExplorer.Services;

public class KioskController : IKioskController
{
    private const double CaptionHeight = 44;

    public static KioskController Instance { get; } = new();

    public bool IsKioskModeEnabled { get; set; }
    public bool PreventAppExit { get; set; }

    public void Configure(AppConfiguration configuration)
    {
        IsKioskModeEnabled = configuration.KioskModeEnabled;
        PreventAppExit = configuration.PreventAppExit;
    }

    public void ApplyKioskWindowSettings(Window? window)
    {
        if (window == null) return;

        if (IsKioskModeEnabled)
        {
            window.WindowState = WindowState.Normal;
            window.WindowStyle = WindowStyle.None;
            window.ResizeMode = ResizeMode.NoResize;
            window.WindowState = WindowState.Maximized;
        }
        else
        {
            window.WindowStyle = WindowStyle.SingleBorderWindow;
            window.ResizeMode = ResizeMode.CanResize;
            window.WindowState = WindowState.Normal;
        }

        if (WindowChrome.GetWindowChrome(window) is { } chrome)
        {
            // Dragging or double-clicking the caption would restore the full-screen window.
            var adjusted = (WindowChrome)chrome.Clone();
            adjusted.CaptionHeight = IsKioskModeEnabled ? 0 : CaptionHeight;
            WindowChrome.SetWindowChrome(window, adjusted);
        }

        AuditService.Instance.Log(
            AuditEventType.KioskLockdownChanged,
            "WindowSettings",
            $"KioskMode: {IsKioskModeEnabled}, PreventExit: {PreventAppExit}",
            true);
    }

    public bool HandleWindowClosing(Window? window, CancelEventArgs e, Func<bool>? authenticateAdmin)
    {
        if (!PreventAppExit)
        {
            return true;
        }

        if (authenticateAdmin != null && authenticateAdmin())
        {
            // The window is already closing; calling Close again would throw.
            PreventAppExit = false;
            AuditService.Instance.Log(AuditEventType.AdminAccess, "AppExit", "Admin authorized application shutdown", true);
            return true;
        }

        e.Cancel = true;
        AuditService.Instance.Log(AuditEventType.SecurityBlocked, "AppExit", "Unauthorized attempt to close kiosk application", false);
        return false;
    }
}
