using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace LiteExplorer.Helpers.WinAPI;

public static class Interop
{
    private const uint FILE_READ_EA = 0x0008;
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_SHARE_READ = 1;
    private const uint FILE_SHARE_WRITE = 2;
    private const uint FILE_SHARE_DELETE = 4;

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SHGetFileInfo(
        string path,
        uint attributes,
        out ShellFileInfo fileInfo,
        uint size,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(IntPtr pointer);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetVolumeInformation(
        string rootPathName,
        StringBuilder volumeNameBuffer,
        int volumeNameSize,
        out uint volumeSerialNumber,
        out uint maximumComponentLength,
        out uint fileSystemFlags,
        StringBuilder fileSystemNameBuffer,
        int fileSystemNameSize);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern uint GetFinalPathNameByHandle(
        SafeFileHandle hFile,
        StringBuilder lpszFilePath,
        uint cchFilePath,
        uint dwFlags);

    /// <summary>
    /// Retrieves the volume serial number in standard format (e.g. "ABCD-1234").
    /// </summary>
    public static string GetVolumeSerialNumber(string rootPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(rootPath)) return null;
            if (!rootPath.EndsWith('\\')) rootPath += "\\";

            var volumeName = new StringBuilder(261);
            var fileSystemName = new StringBuilder(261);
            if (GetVolumeInformation(rootPath, volumeName, volumeName.Capacity, out uint serialNumber, out _, out _, fileSystemName, fileSystemName.Capacity))
            {
                return $"{serialNumber >> 16:X4}-{serialNumber & 0xFFFF:X4}";
            }
        }
        catch
        {
        }
        return null;
    }

    /// <summary>
    /// Resolves the canonical path on disk by resolving symlinks, junctions, and mount points.
    /// </summary>
    public static string GetFinalPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;

        try
        {
            using var handle = CreateFile(
                path,
                FILE_READ_EA,
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                IntPtr.Zero,
                OPEN_EXISTING,
                FILE_FLAG_BACKUP_SEMANTICS,
                IntPtr.Zero);

            if (handle != null && !handle.IsInvalid)
            {
                var sb = new StringBuilder(1024);
                uint res = GetFinalPathNameByHandle(handle, sb, (uint)sb.Capacity, 0);
                if (res > 0)
                {
                    string result = sb.ToString();
                    if (result.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
                    {
                        return @"\\" + result.Substring(8);
                    }
                    if (result.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
                    {
                        return result.Substring(4);
                    }
                    return result;
                }
            }
        }
        catch
        {
        }

        return path;
    }
}
