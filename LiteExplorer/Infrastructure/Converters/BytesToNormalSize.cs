using LiteExplorer.Infrastructure.Converters.Base;
using System;
using System.Globalization;

namespace LiteExplorer.Infrastructure.Converters;

internal class BytesToNormalSize : Converter
{
    private const double BytesInKB = 1024.0;
    private const double BytesInMB = 1048576.0;
    private const double BytesInGB = 1073741824.0;
    private const double NotApplicableSentinel = 12345667658675846.0;

    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return string.Empty;

        double spaceInBytes;
        try
        {
            spaceInBytes = System.Convert.ToDouble(value);
        }
        catch
        {
            return string.Empty;
        }

        if (spaceInBytes < 0 || Math.Abs(spaceInBytes - NotApplicableSentinel) < 1.0)
        {
            return string.Empty;
        }

        if (spaceInBytes + 4096 >= BytesInGB)
            return $"{spaceInBytes / BytesInGB:0.00} GB";
        if (spaceInBytes >= BytesInMB)
            return $"{spaceInBytes / BytesInMB:0.0} MB";
        if (spaceInBytes >= BytesInKB)
            return $"{spaceInBytes / BytesInKB:0} KB";

        return $"{spaceInBytes:0} B";
    }
}
