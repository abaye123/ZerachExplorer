using LiteExplorer.Helpers.Enums;
using LiteExplorer.Helpers.WinAPI;
using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LiteExplorer.Helpers;

public static class FileManager
{
    private static readonly ConcurrentDictionary<string, ImageSource> IconCache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource GetImageSource(string filename)
    {
        return GetImageSource(filename, new Size(20, 20));
    }

    public static ImageSource GetImageSource(string filename, Size size)
    {
        string extension = string.IsNullOrWhiteSpace(filename) ? "" : Path.GetExtension(filename).ToLowerInvariant();
        string cacheKey = $"{extension}_{size.Width}x{size.Height}";

        return IconCache.GetOrAdd(cacheKey, _ =>
        {
            try
            {
                using var icon = ShellManager.GetIcon(extension, ItemType.File, IconSize.Small, ItemState.Undefined);
                if (icon == null || icon.Handle == IntPtr.Zero)
                    return null;

                var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(size.Width, size.Height));

                if (bitmapSource.CanFreeze)
                {
                    bitmapSource.Freeze();
                }

                return bitmapSource;
            }
            catch
            {
                return null;
            }
        });
    }

    public static void ClearCache() => IconCache.Clear();
}
