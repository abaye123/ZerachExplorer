using LiteExplorer.Infrastructure.Converters.Base;
using System;
using System.Globalization;
using System.Windows;

namespace LiteExplorer.Infrastructure.Converters;

internal class InverseBoolToVisibility : Converter
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool predicate && !predicate ? Visibility.Visible : Visibility.Collapsed;
    }
}
