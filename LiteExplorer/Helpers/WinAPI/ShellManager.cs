using LiteExplorer.Helpers.Enums;
using System;
using System.Collections.Concurrent;
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
}
