using LiteExplorer.Helpers.Enums;
using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Runtime.InteropServices;

namespace LiteExplorer.Helpers.WinAPI;

public static class ShellManager
{
    private static readonly ConcurrentDictionary<string, string> TypeNameCache = new(StringComparer.OrdinalIgnoreCase);

    public static string GetFileTypeDescription(string extension)
    {
        var key = string.IsNullOrEmpty(extension) ? "." : extension;
        return TypeNameCache.GetOrAdd(key, ext =>
        {
            var dummyName = ext == "." ? "file" : "file" + ext;
            var fileInfo = new ShellFileInfo();
            var size = (uint)Marshal.SizeOf(fileInfo);
            var flags = (uint)(ShellAttribute.TypeName | ShellAttribute.UseFileAttributes);
            var result = Interop.SHGetFileInfo(dummyName, (uint)FileAttribute.File, out fileInfo, size, flags);

            if (result != IntPtr.Zero && !string.IsNullOrWhiteSpace(fileInfo.szTypeName))
                return fileInfo.szTypeName;

            if (!string.IsNullOrEmpty(ext) && ext != ".")
                return $"קובץ {ext.TrimStart('.').ToUpperInvariant()}";

            return "קובץ";
        });
    }

    public static Icon GetIcon(string path, ItemType type, IconSize iconSize, ItemState state)
    {
        var attributes = (uint)(type == ItemType.Folder ? FileAttribute.Directory : FileAttribute.File);
        var flags = (uint)(ShellAttribute.Icon | ShellAttribute.UseFileAttributes);

        if (type == ItemType.Folder && state == ItemState.Open)
            flags |= (uint)ShellAttribute.OpenIcon;

        if (iconSize == IconSize.Small)
            flags |= (uint)ShellAttribute.SmallIcon;
        else
            flags |= (uint)ShellAttribute.LargeIcon;

        var fileInfo = new ShellFileInfo();
        var size = (uint)Marshal.SizeOf(fileInfo);
        var result = Interop.SHGetFileInfo(path, attributes, out fileInfo, size, flags);

        if (result == IntPtr.Zero || fileInfo.hIcon == IntPtr.Zero)
            return null;

        try
        {
            return (Icon)Icon.FromHandle(fileInfo.hIcon).Clone();
        }
        finally
        {
            Interop.DestroyIcon(fileInfo.hIcon);
        }
    }
}
