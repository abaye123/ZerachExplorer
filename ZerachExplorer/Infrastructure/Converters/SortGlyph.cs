using ZerachExplorer.Helpers;
using ZerachExplorer.Infrastructure.Converters.Base;
using System.ComponentModel;
using System.Globalization;

namespace ZerachExplorer.Infrastructure.Converters;

/// <summary>The arrow of a column header; values: sorted column, direction, the header's column.</summary>
internal class SortGlyph : MultiConverter
{
    public override object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is not [SortColumn column, ListSortDirection direction, string tag] || column.ToString() != tag)
            return string.Empty;

        return direction == ListSortDirection.Ascending ? "\uE70E" : "\uE70D";
    }
}
