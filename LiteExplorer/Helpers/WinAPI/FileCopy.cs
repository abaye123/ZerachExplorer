using LiteExplorer.Services;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace LiteExplorer.Helpers.WinAPI;

/// <summary>Copies through Windows, which keeps attributes and alternate streams, with progress, pause and cancel.</summary>
internal static class FileCopy
{
    private const uint FailIfExists = 1;
    private const int ErrorRequestAborted = 1235;

    private enum Answer : uint
    {
        Continue = 0,
        Cancel = 1
    }

    private delegate Answer ProgressRoutine(long totalSize, long transferred, long streamSize, long streamTransferred,
        uint streamNumber, uint reason, IntPtr source, IntPtr destination, IntPtr data);

    [DllImport("kernel32.dll", EntryPoint = "CopyFileExW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CopyFileEx(string source, string destination, ProgressRoutine routine, IntPtr data, IntPtr cancel, uint flags);

    /// <exception cref="OperationCanceledException">Cancelled; the partial file is removed by Windows.</exception>
    /// <exception cref="IOException">The copy failed.</exception>
    public static void Copy(string source, string destination, OperationProgress progress)
    {
        ProgressRoutine routine = (_, transferred, _, _, _, _, _, _, _) =>
        {
            progress.ReportFileBytes(transferred);
            try
            {
                progress.Checkpoint();
                return Answer.Continue;
            }
            catch (OperationCanceledException)
            {
                // Exceptions must not cross into native code.
                return Answer.Cancel;
            }
        };

        bool copied = CopyFileEx(source, destination, routine, IntPtr.Zero, IntPtr.Zero, FailIfExists);
        int error = Marshal.GetLastWin32Error();
        GC.KeepAlive(routine);

        if (copied)
            return;
        if (error == ErrorRequestAborted)
            throw new OperationCanceledException();
        throw new IOException(new Win32Exception(error).Message, unchecked((int)0x80070000) | error);
    }
}
