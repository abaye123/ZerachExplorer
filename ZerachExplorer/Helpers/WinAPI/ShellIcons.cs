using ZerachExplorer.Helpers.Enums;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ZerachExplorer.Helpers.WinAPI;

/// <summary>Both sizes come from large system images, so they stay sharp on scaled displays.</summary>
public sealed record ShellIcon(ImageSource? Small, ImageSource? Large);

public static class ShellIcons
{
    private const uint SystemIconIndex = 0x4000;
    private const int ExtraLargeList = 2;
    private const int JumboList = 4;
    private const int ExtraLargePixels = 48;
    private const double LargePixels = 96;
    private const int DrawTransparent = 1;

    private static readonly ShellIcon Missing = new(null, null);
    private static readonly ConcurrentDictionary<string, ShellIcon> Cache = new(StringComparer.OrdinalIgnoreCase);

    [ComImport, Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageList
    {
        void Add();
        void ReplaceIcon();
        void SetOverlayImage();
        void Replace();
        void AddMasked();
        void Draw();
        void Remove();

        [PreserveSig]
        int GetIcon(int index, int flags, out IntPtr icon);
    }

    [DllImport("shell32.dll", EntryPoint = "#727")]
    private static extern int SHGetImageList(int list, in Guid interfaceId, out IImageList? imageList);

    public static ShellIcon ForFile(string path)
    {
        string extension = Path.GetExtension(path);
        return Get("file:" + extension, "file" + extension, FileAttribute.File);
    }

    public static ShellIcon ForFolder() => Get("folder", "folder", FileAttribute.Directory);

    /// <summary>The icon of a drive or special folder, which Windows chooses for that exact location.</summary>
    public static ShellIcon ForLocation(string path) => Get("location:" + path, path, null);

    private static ShellIcon Get(string key, string path, FileAttribute? attributes)
    {
        if (Cache.TryGetValue(key, out var cached))
            return cached;

        var icon = Load(path, attributes);
        // A failure may be temporary, for example a drive that is still being mounted.
        if (icon.Small != null)
            Cache[key] = icon;
        return icon;
    }

    private static ShellIcon Load(string path, FileAttribute? attributes)
    {
        uint flags = SystemIconIndex | (attributes == null ? 0 : (uint)ShellAttribute.UseFileAttributes);
        if (Interop.SHGetFileInfo(path, (uint)(attributes ?? 0), out var info, (uint)Marshal.SizeOf<ShellFileInfo>(), flags) == IntPtr.Zero)
            return Missing;

        var small = LoadImage(ExtraLargeList, info.iIcon);
        var jumbo = LoadImage(JumboList, info.iIcon);
        var large = jumbo != null && FillsFrame(jumbo) ? Shrink(jumbo) : small;
        return new ShellIcon(small, large);
    }

    private static BitmapSource? LoadImage(int list, int index)
    {
        if (SHGetImageList(list, typeof(IImageList).GUID, out var images) != 0 || images == null)
            return null;

        IntPtr handle = IntPtr.Zero;
        try
        {
            if (images.GetIcon(index, DrawTransparent, out handle) != 0 || handle == IntPtr.Zero)
                return null;

            var image = Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            if (handle != IntPtr.Zero)
                Interop.DestroyIcon(handle);
            Marshal.ReleaseComObject(images);
        }
    }

    /// <summary>Icons without a large version are placed unscaled in the corner of the large frame.</summary>
    private static bool FillsFrame(BitmapSource image)
    {
        var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        int stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);

        for (int y = 0; y < converted.PixelHeight; y++)
        {
            for (int x = 0; x < converted.PixelWidth; x++)
            {
                if ((x > ExtraLargePixels || y > ExtraLargePixels) && pixels[y * stride + x * 4 + 3] != 0)
                    return true;
            }
        }
        return false;
    }

    private static BitmapSource Shrink(BitmapSource image)
    {
        double scale = LargePixels / image.PixelWidth;
        var result = new CachedBitmap(new TransformedBitmap(image, new ScaleTransform(scale, scale)), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        result.Freeze();
        return result;
    }
}
