using System;
using System.ComponentModel;
using System.Windows;

namespace LiteExplorer.Services;

public interface IKioskController
{
    bool IsKioskModeEnabled { get; set; }
    bool PreventAppExit { get; set; }
    void ApplyKioskWindowSettings(Window window);
    bool HandleWindowClosing(Window window, CancelEventArgs e, Func<bool> authenticateAdmin);
}
