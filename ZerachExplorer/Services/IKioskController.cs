using System;
using System.ComponentModel;
using System.Windows;

namespace ZerachExplorer.Services;

public interface IKioskController
{
    bool IsKioskModeEnabled { get; set; }
    bool PreventAppExit { get; set; }
    void Configure(ZerachExplorer.MVVM.Models.AppConfiguration configuration);
    void ApplyKioskWindowSettings(Window? window);
    bool HandleWindowClosing(Window? window, CancelEventArgs e, Func<bool>? authenticateAdmin);
}
