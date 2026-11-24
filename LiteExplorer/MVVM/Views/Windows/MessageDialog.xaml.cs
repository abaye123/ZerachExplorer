using LiteExplorer.Infrastructure.Converters;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;

namespace LiteExplorer.MVVM.Views.Windows;

public enum MessageKind
{
    Info,
    Warning,
    Error,
    Question
}

/// <summary>Messages and confirmations in the style of the application.</summary>
public partial class MessageDialog : DialogWindow
{
    public static IValueConverter LineDirection { get; } = new LineDirectionConverter();

    public MessageDialog(string message, string title, MessageKind kind, string acceptText = "אישור")
    {
        InitializeComponent();
        Title = title;
        // The marks keep ".exe" intact inside a Hebrew sentence.
        Lines.ItemsSource = Extension().Replace(message.ReplaceLineEndings("\n"), "\u200E$0\u200E").Split('\n');
        BtnAccept.Content = acceptText;

        (Glyph, string brush) = kind switch
        {
            MessageKind.Warning => ("\uE7BA", "WarningBrush"),
            MessageKind.Error => ("\uEA39", "ErrorBrush"),
            MessageKind.Question => ("\uE9CE", "WarningBrush"),
            _ => ("\uE946", "AccentBrush")
        };
        SetResourceReference(GlyphBrushProperty, brush);

        if (kind == MessageKind.Question)
        {
            // Enter must not confirm by accident.
            BtnCancel.Visibility = Visibility.Visible;
            BtnCancel.IsDefault = true;
            Loaded += (_, _) => BtnCancel.Focus();
        }
        else
        {
            BtnAccept.IsDefault = BtnAccept.IsCancel = true;
            Loaded += (_, _) => BtnAccept.Focus();
        }
    }

    public static bool Show(Window? owner, string message, string title, MessageKind kind, string acceptText = "אישור")
    {
        var dialog = new MessageDialog(message, title, kind, acceptText);
        if (owner is { IsVisible: true })
            dialog.Owner = owner;
        else
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return dialog.ShowDialog() == true;
    }

    private void Accept_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    [GeneratedRegex(@"(?<![\w\\])\.[A-Za-z0-9]+")]
    private static partial Regex Extension();

    private sealed partial class LineDirectionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string line = value as string ?? string.Empty;
            // A path stays left to right even with Hebrew folder names; any other Hebrew line is a sentence.
            if (PathLine().IsMatch(line))
                return FlowDirection.LeftToRight;
            return line.Any(c => TextDirection.Of(c.ToString()) == FlowDirection.RightToLeft) ? FlowDirection.RightToLeft : TextDirection.Of(line);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();

        [GeneratedRegex(@"^[A-Za-z]:\\")]
        private static partial Regex PathLine();
    }
}
