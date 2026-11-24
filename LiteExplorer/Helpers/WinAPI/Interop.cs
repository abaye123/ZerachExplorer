using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
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
    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;

    private const uint FSCTL_LOCK_VOLUME = 0x00090018;
    private const uint FSCTL_DISMOUNT_VOLUME = 0x00090020;
    private const uint IOCTL_STORAGE_EJECT_MEDIA = 0x002D4808;

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

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    /// <summary>
    /// Retrieves the volume serial number in standard format (e.g. "ABCD-1234").
    /// </summary>
    public static string? GetVolumeSerialNumber(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath)) return null;
        if (!rootPath.EndsWith('\\')) rootPath += "\\";

        var volumeName = new StringBuilder(261);
        var fileSystemName = new StringBuilder(261);
        return GetVolumeInformation(rootPath, volumeName, volumeName.Capacity, out uint serialNumber, out _, out _, fileSystemName, fileSystemName.Capacity)
            ? $"{serialNumber >> 16:X4}-{serialNumber & 0xFFFF:X4}"
            : null;
    }

    /// <summary>
    /// Resolves the canonical path on disk by resolving symlinks, junctions, and mount points.
    /// </summary>
    public static string GetFinalPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;

        using var handle = CreateFile(
            path,
            FILE_READ_EA,
            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_FLAG_BACKUP_SEMANTICS,
            IntPtr.Zero);

        if (handle.IsInvalid)
            return path;

        var buffer = new StringBuilder(1024);
        uint length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
        if (length >= buffer.Capacity)
        {
            // Too small: the returned value is the required size.
            buffer = new StringBuilder((int)length + 1);
            length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
        }

        if (length == 0)
            return path;

        string result = buffer.ToString();
        if (result.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            return @"\\" + result.Substring(8);

        return result.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase) ? result.Substring(4) : result;
    }

    /// <summary>
    /// Safely locks, dismounts, and ejects a removable volume via Windows Storage IOCTLs.
    /// </summary>
    public static bool EjectVolume(string driveLetter, [NotNullWhen(false)] out string? errorMessage)
    {
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(driveLetter))
        {
            errorMessage = "אות הכונן ריקה.";
            return false;
        }

        string cleanLetter = driveLetter.Trim().TrimEnd('\\', '/');
        string devicePath = @"\\.\" + cleanLetter;

        SafeFileHandle handle = CreateFile(
            devicePath,
            GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            // Retry with read access only if write access is denied.
            handle = CreateFile(
                devicePath,
                GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);
        }

        if (handle.IsInvalid)
        {
            int err = Marshal.GetLastWin32Error();
            errorMessage = new Win32Exception(err).Message;
            return false;
        }

        using (handle)
        {
            // Never dismount without the lock: a forced dismount invalidates files
            // that other programs still have open and can lose unsaved data.
            bool locked = false;
            int lockError = 0;
            for (int i = 0; i < 5 && !locked; i++)
            {
                if (i > 0)
                    System.Threading.Thread.Sleep(200);

                locked = DeviceIoControl(handle, FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);
                if (!locked)
                    lockError = Marshal.GetLastWin32Error();
            }

            if (!locked)
            {
                errorMessage = new Win32Exception(lockError).Message;
                return false;
            }

            if (!DeviceIoControl(handle, FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
            {
                errorMessage = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                return false;
            }

            // Send eject request to hardware layer.
            if (DeviceIoControl(handle, IOCTL_STORAGE_EJECT_MEDIA, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
            {
                return true;
            }

            int lastErr = Marshal.GetLastWin32Error();
            errorMessage = new Win32Exception(lastErr).Message;
            return false;
        }
    }

    private const uint FO_DELETE = 3;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_WANTNUKEWARNING = 0x4000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOpStruct
    {
        public IntPtr Window;
        public uint Function;
        public string From;
        public string? To;
        public ushort Flags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool AnyOperationsAborted;
        public IntPtr NameMappings;
        public string? ProgressTitle;
    }

    [DllImport("shell32.dll", EntryPoint = "SHFileOperationW", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref ShFileOpStruct operation);

    /// <summary>
    /// Sends a file or folder to the recycle bin. The shell itself warns before any
    /// permanent deletion, for example when the item is too large for the bin.
    /// </summary>
    public static int SendToRecycleBin(string path, out bool aborted)
    {
        var operation = new ShFileOpStruct
        {
            Function = FO_DELETE,
            From = path + "\0\0",
            Flags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_WANTNUKEWARNING
        };

        int result = SHFileOperation(ref operation);
        aborted = operation.AnyOperationsAborted;
        return result;
    }
}
