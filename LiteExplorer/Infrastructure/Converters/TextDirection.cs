using LiteExplorer.Infrastructure.Converters.Base;
using System.Globalization;
using System.Windows;

namespace LiteExplorer.Infrastructure.Converters;

/// <summary>Direction of a text by its first letter, so Latin names and paths are not scrambled.</summary>
internal class TextDirection : Converter
{
    public static FlowDirection Of(string? text)
    {
        foreach (char c in text ?? string.Empty)
        {
            if (c is >= '\u0590' and <= '\u08FF')
                return FlowDirection.RightToLeft;
            if (char.IsLetter(c))
                return FlowDirection.LeftToRight;
        }
        return FlowDirection.LeftToRight;
    }

    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Of(value as string);
}
