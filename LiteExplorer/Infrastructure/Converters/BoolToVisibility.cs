using LiteExplorer.Infrastructure.Converters.Base;
using System;
using System.Globalization;
using System.Windows;

namespace LiteExplorer.Infrastructure.Converters;

internal class BoolToVisibility : Converter
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool predicate)
        {
            if (parameter is string param && string.Equals(param, "Inverse", StringComparison.OrdinalIgnoreCase))
            {
                predicate = !predicate;
            }

            return predicate ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }
}
