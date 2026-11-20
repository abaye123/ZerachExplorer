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

public static class FolderManager
{
    private static readonly ConcurrentDictionary<string, ImageSource> FolderIconCache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource GetImageSource(string directory, ItemState folderType)
    {
        return GetImageSource(directory, new Size(20, 20), folderType);
    }

    public static ImageSource GetImageSource(string directory, Size size, ItemState folderType)
    {
        bool isDrive = !string.IsNullOrEmpty(directory) && Path.GetPathRoot(directory) == directory;
        string cacheKey = isDrive 
            ? $"drive_{directory}_{size.Width}x{size.Height}" 
            : $"folder_{(int)folderType}_{size.Width}x{size.Height}";

        return FolderIconCache.GetOrAdd(cacheKey, _ =>
        {
            try
            {
                using var icon = ShellManager.GetIcon(directory, ItemType.Folder, IconSize.Large, folderType);
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

    public static void ClearCache() => FolderIconCache.Clear();
}
