using LiteExplorer.Infrastructure.Converters;
using System.Windows;

namespace LiteExplorer.MVVM.Views.Windows;

public partial class TextInputDialog : DialogWindow
{
    private TextInputDialog(string prompt, string title, string initialValue)
    {
        InitializeComponent();
        Title = title;
        TxtPrompt.Text = prompt;
        TxtValue.TextChanged += (_, _) => TxtValue.FlowDirection = TextDirection.Of(TxtValue.Text);
        TxtValue.Text = initialValue;
        Loaded += (_, _) =>
        {
            TxtValue.Focus();
            TxtValue.SelectAll();
        };
    }

    /// <summary>The entered text, or null when cancelled or empty.</summary>
    public static string? Prompt(Window? owner, string prompt, string title, string initialValue)
    {
        var dialog = new TextInputDialog(prompt, title, initialValue) { Owner = owner };
        string value = dialog.ShowDialog() == true ? dialog.TxtValue.Text.Trim() : string.Empty;
        return value.Length == 0 ? null : value;
    }

    private void Accept_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
