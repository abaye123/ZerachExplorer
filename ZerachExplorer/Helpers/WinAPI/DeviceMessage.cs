using System.Runtime.InteropServices;

namespace ZerachExplorer.Helpers.WinAPI;

/// <summary>Reads WM_DEVICECHANGE messages about drives being connected or removed.</summary>
internal static class DeviceMessage
{
    public const int Id = 0x0219;

    private const int Arrival = 0x8000;
    private const int RemoveComplete = 0x8004;
    private const int VolumeType = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct Volume
    {
        public int Size;
        public int DeviceType;
        public int Reserved;
        public uint UnitMask;
        public short Flags;
    }

    public static bool TryParse(IntPtr wParam, IntPtr lParam, out bool arrived, out IReadOnlyList<string> roots)
    {
        arrived = wParam == Arrival;
        roots = Array.Empty<string>();
        if ((!arrived && wParam != RemoveComplete) || lParam == IntPtr.Zero)
            return false;

        // Other device types have a different layout after the common header.
        if (Marshal.ReadInt32(lParam, sizeof(int)) != VolumeType)
            return false;

        roots = ToRoots(Marshal.PtrToStructure<Volume>(lParam).UnitMask);
        return roots.Count > 0;
    }

    internal static IReadOnlyList<string> ToRoots(uint unitMask) =>
        Enumerable.Range(0, 26).Where(i => (unitMask & (1u << i)) != 0).Select(i => $"{(char)('A' + i)}:\\").ToList();
}
