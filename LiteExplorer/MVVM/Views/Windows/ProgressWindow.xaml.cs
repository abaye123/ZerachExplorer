using LiteExplorer.Infrastructure.Converters.Base;
using LiteExplorer.MVVM.ViewModels;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;

namespace LiteExplorer.MVVM.Views.Windows;

/// <summary>Running operations. Closing only hides it; the status bar of the main window brings it back.</summary>
public partial class ProgressWindow : DialogWindow
{
    private readonly OperationsViewModel operations;

    internal ProgressWindow(OperationsViewModel operations)
    {
        this.operations = operations;
        InitializeComponent();
        DataContext = operations;
        operations.ShowRequested += OnShowRequested;
        operations.PropertyChanged += OnOperationsChanged;
    }

    public static IMultiValueConverter FractionWidth { get; } = new FractionWidthConverter();

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (Owner is not MainWindow { IsClosingNow: false }) return;

        e.Cancel = true;
        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        operations.ShowRequested -= OnShowRequested;
        operations.PropertyChanged -= OnOperationsChanged;
    }

    private void OnShowRequested()
    {
        if (operations.HasVisible)
            Show();
    }

    private void OnOperationsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OperationsViewModel.HasVisible) && !operations.HasVisible)
            Hide();
    }

    private sealed class FractionWidthConverter : MultiConverter
    {
        public override object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
            values is [double fraction, double width] ? Math.Clamp(fraction, 0, 1) * width : 0.0;
    }
}
