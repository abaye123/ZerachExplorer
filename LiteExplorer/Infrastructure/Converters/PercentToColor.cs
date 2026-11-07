using LiteExplorer.Infrastructure.Converters.Base;
using System;
using System.Globalization;
using System.Windows.Media;

namespace LiteExplorer.Infrastructure.Converters;

internal class PercentToColor : Converter
{
    private static readonly SolidColorBrush DangerBrush = CreateFrozenBrush(150, 50, 0);
    private static readonly SolidColorBrush WarningBrush = CreateFrozenBrush(150, 150, 50);
    private static readonly SolidColorBrush ZeroBrush = CreateFrozenBrush(255, 255, 255);
    private static readonly SolidColorBrush InfoBrush = CreateFrozenBrush(50, 150, 50);

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return InfoBrush;

        double percent;
        try
        {
            percent = System.Convert.ToDouble(value);
        }
        catch
        {
            return InfoBrush;
        }

        if (percent > 90)
            return DangerBrush;
        if (percent > 70)
            return WarningBrush;
        if (percent == 0)
            return ZeroBrush;

        return InfoBrush;
    }
}
