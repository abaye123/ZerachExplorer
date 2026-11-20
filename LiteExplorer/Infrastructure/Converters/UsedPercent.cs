using LiteExplorer.Infrastructure.Converters.Base;
using System;
using System.Globalization;

namespace LiteExplorer.Infrastructure.Converters;

internal class UsedPercent : MultiConverter
{
    public override object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2 || values[0] == null || values[1] == null)
            return 0.0;

        try
        {
            var total = System.Convert.ToDouble(values[0]);
            var used = System.Convert.ToDouble(values[1]);
            return total > 0 ? (used / total * 100.0) : 0.0;
        }
        catch
        {
            return 0.0;
        }
    }
}
