using LiteExplorer.MVVM.Models;
using System;
using System.Collections;
using System.ComponentModel;

namespace LiteExplorer.Helpers;

public enum SortColumn
{
    Name,
    LastWriteTime,
    Type,
    Size
}

internal class FileSystemObjectComparer : IComparer
{
    private readonly SortColumn column;
    private readonly ListSortDirection direction;

    public FileSystemObjectComparer(SortColumn column, ListSortDirection direction)
    {
        this.column = column;
        this.direction = direction;
    }

    public int Compare(object? x, object? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is not FileSystemObject itemA) return -1;
        if (y is not FileSystemObject itemB) return 1;

        // Directories always appear before files
        if (itemA.IsDirectory && !itemB.IsDirectory)
            return -1;
        if (!itemA.IsDirectory && itemB.IsDirectory)
            return 1;

        int result = column switch
        {
            SortColumn.Name => string.Compare(itemA.Name, itemB.Name, StringComparison.CurrentCultureIgnoreCase),
            SortColumn.LastWriteTime => CompareDates(itemA, itemB),
            SortColumn.Type => string.Compare(itemA.Type ?? string.Empty, itemB.Type ?? string.Empty, StringComparison.CurrentCultureIgnoreCase),
            SortColumn.Size => itemA.Size.CompareTo(itemB.Size),
            _ => string.Compare(itemA.Name, itemB.Name, StringComparison.CurrentCultureIgnoreCase)
        };

        if (result == 0)
        {
            result = string.Compare(itemA.Name, itemB.Name, StringComparison.CurrentCultureIgnoreCase);
        }

        return direction == ListSortDirection.Ascending ? result : -result;
    }

    private static int CompareDates(FileSystemObject a, FileSystemObject b)
    {
        if (a.RawLastWriteTime.HasValue && b.RawLastWriteTime.HasValue)
            return a.RawLastWriteTime.Value.CompareTo(b.RawLastWriteTime.Value);

        return string.Compare(a.LastWriteTime ?? string.Empty, b.LastWriteTime ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
