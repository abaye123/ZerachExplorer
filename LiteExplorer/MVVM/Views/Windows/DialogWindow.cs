using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace LiteExplorer.MVVM.Views.Windows;

/// <summary>Common frame of all dialogs: title with an icon, content, and a row of buttons.</summary>
public class DialogWindow : Window
{
    private const int CornerPreference = 33;
    private const int RoundCorners = 2;

    public static readonly DependencyProperty FooterProperty =
        DependencyProperty.Register(nameof(Footer), typeof(object), typeof(DialogWindow));

    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(DialogWindow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty GlyphBrushProperty =
        DependencyProperty.Register(nameof(GlyphBrush), typeof(Brush), typeof(DialogWindow));

    public DialogWindow()
    {
        SetResourceReference(StyleProperty, "DialogWindow");
        SetResourceReference(GlyphBrushProperty, "AccentBrush");
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (_, _) => Close()));
    }

    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    /// <summary>One character of the system icon font.</summary>
    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public Brush? GlyphBrush
    {
        get => (Brush?)GetValue(GlyphBrushProperty);
        set => SetValue(GlyphBrushProperty, value);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Windows 10 has no rounded corners and ignores the request.
        int preference = RoundCorners;
        _ = DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, CornerPreference, ref preference, sizeof(int));

        // The custom frame is applied after the first measure and would leave an empty strip.
        InvalidateMeasure();
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
