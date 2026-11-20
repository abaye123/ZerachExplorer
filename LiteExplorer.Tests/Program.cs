using LiteExplorer.Helpers;
using LiteExplorer.MVVM.ViewModels;
using LiteExplorer.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LiteExplorer.Tests;

public class Program
{
    public static async Task<int> Main()
    {
        Console.WriteLine("Running LiteExplorer Test Suite...");
        int passed = 0;
        int failed = 0;

        void Assert(bool condition, string testName, string details = "")
        {
            if (condition)
            {
                Console.WriteLine($"[PASS] {testName}");
                passed++;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] {testName} - {details}");
                Console.ResetColor();
                failed++;
            }
        }

        var policy = new PathPolicy();

        // System drive navigation protection
        Assert(!policy.CanNavigate(@"C:\", out _), "Block uppercase C:\\ navigation");
        Assert(!policy.CanNavigate(@"c:\", out _), "Block lowercase c:\\ navigation");
        Assert(!policy.CanNavigate(@"c:\windows", out _), "Block lowercase c:\\windows navigation");
        Assert(!policy.CanNavigate(@"C:\Windows\System32", out _), "Block C:\\Windows\\System32 navigation");
        Assert(!policy.CanNavigate(PathPolicy.SystemDriveRoot, out _), "Block dynamic SystemDriveRoot");

        // Executable and system file execution prevention
        Assert(!policy.CanOpenFile(@"c:\windows\notepad.exe", out _), "Block opening executable on C:");
        Assert(!policy.CanOpenFile(@"c:\temp\document.docx", out _), "Block opening document on C:");
        Assert(!policy.CanOpenFile(@"c:\temp\document.pdf", out _), "Block opening pdf on C:");

        // Special paths, UNC and device namespace protection
        Assert(!policy.CanNavigate(@"\\localhost\c$", out _), "Block UNC network path \\\\localhost\\c$");
        Assert(!policy.CanNavigate(@"\\?\C:\Windows", out _), "Block NT namespace \\\\?\\C:\\Windows");
        Assert(!policy.CanNavigate(@"\\.\PhysicalDrive0", out _), "Block device path \\\\.\\PhysicalDrive0");
        Assert(!policy.CanNavigate(@"//server/share", out _), "Block forward-slash UNC //server/share");
        Assert(!policy.CanOpenFile(@"\\server\share\file.docx", out _), "Block UNC file opening");
        Assert(!policy.CanNavigate(@"D:\CON", out _), "Block reserved DOS device CON");
        Assert(!policy.CanNavigate(@"D:\NUL\file.txt", out _), "Block reserved DOS device NUL in path");

        // Rename boundary and path traversal protection
        Assert(!policy.CanRename(@"D:\docs\file.docx", @"D:\docs\..\..\evil.docx", out _), "Block path traversal ..\\.. in rename");
        Assert(!policy.CanRename(@"D:\docs\file.docx", @"D:\other\file.docx", out _), "Block moving file to another directory in rename");
        Assert(!policy.CanRename(@"D:\docs\file.docx", @"D:\docs\file.exe", out _), "Block extension change in rename");

        // Drive root deletion protection
        Assert(!policy.CanDelete(@"C:\", out _), "Block delete drive root C:\\");
        Assert(!policy.CanDelete(@"c:\", out _), "Block delete drive root c:\\");
        Assert(!policy.CanDelete(@"D:\", out _), "Block delete drive root D:\\");
        Assert(!policy.CanDelete(@"D:", out _), "Block delete drive root D:");
        Assert(!policy.CanDelete(@"E:\", out _), "Block delete drive root E:\\");

        // Drive whitelisting and restrictions
        var customPolicy = new PathPolicy();
        Assert(!customPolicy.AllowedDriveRoots.Contains(@"D:\"), "Whitelist default excludes non-removable drives");
        customPolicy.AllowedDriveRoots.Add(@"D:\");
        Assert(customPolicy.AllowedDriveRoots.Contains(@"D:\"), "Whitelist accepts explicitly allowed drive root");

        // Restricted volume labels
        Assert(customPolicy.BlockedVolumeLabels.Contains("Otzar Hahochma"), "Blocked labels contain Otzar Hahochma");
        Assert(customPolicy.BlockedVolumeLabels.Contains("אוצר החוכמה"), "Blocked labels contain Hebrew label");

        // Allowed and disallowed extensions
        Assert(customPolicy.AllowedExtensions.Contains(".docx"), "Allowed extensions contain .docx");
        Assert(customPolicy.AllowedExtensions.Contains(".pdf"), "Allowed extensions contain .pdf");
        Assert(customPolicy.AllowedExtensions.Contains(".xlsx"), "Allowed extensions contain .xlsx");
        Assert(!customPolicy.AllowedExtensions.Contains(".exe"), "Disallowed extensions do not contain .exe");
        Assert(!customPolicy.AllowedExtensions.Contains(".bat"), "Disallowed extensions do not contain .bat");
        Assert(!customPolicy.AllowedExtensions.Contains(".ps1"), "Disallowed extensions do not contain .ps1");

        // Crash logging verification
        try
        {
            var testEx = new InvalidOperationException("Test unhandled exception verification");
            CrashLogger.LogException(testEx, "Unit Testing Suite");
            CrashLogger.LogMessage("Unit test verification message", "TEST");

            Assert(File.Exists(CrashLogger.LogFilePath), "CrashLogger creates log file on disk");
            string logContent = File.ReadAllText(CrashLogger.LogFilePath);
            Assert(logContent.Contains("[CRASH REPORT]"), "Crash report contains header");
            Assert(logContent.Contains("Test unhandled exception"), "Crash report contains exception message");
            Assert(logContent.Contains("Unit test verification message"), "Crash report contains custom message");
        }
        catch (Exception ex)
        {
            Assert(false, "CrashLogger execution threw unexpected exception", ex.Message);
        }

        // Icon caching operations
        try
        {
            FileManager.ClearCache();
            FolderManager.ClearCache();
            Assert(true, "Icon caches cleared without error");
        }
        catch (Exception ex)
        {
            Assert(false, "Icon cache clear threw exception", ex.Message);
        }

        // Directory size calculation and cancellation token support
        string tempDir = Path.Combine(Path.GetTempPath(), "ZerachExplorerTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            string subDir = Path.Combine(tempDir, "SubDir");
            Directory.CreateDirectory(subDir);

            byte[] bytes1 = new byte[1024];
            byte[] bytes2 = new byte[2048];
            File.WriteAllBytes(Path.Combine(tempDir, "file1.bin"), bytes1);
            File.WriteAllBytes(Path.Combine(subDir, "file2.bin"), bytes2);

            long calculatedSize = await TabContentViewModel.CalculateDirectorySizeAsync(tempDir);
            Assert(calculatedSize == 3072, $"Directory size calculated correctly (expected 3072, got {calculatedSize})");

            using var cts = new CancellationTokenSource();
            cts.Cancel();
            bool wasCancelled = false;
            try
            {
                await TabContentViewModel.CalculateDirectorySizeAsync(tempDir, cts.Token);
            }
            catch (OperationCanceledException)
            {
                wasCancelled = true;
            }
            Assert(wasCancelled, "Directory calculation honors cancellation token");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        Console.WriteLine($"\nTest Results: {passed} passed, {failed} failed.");
        return failed == 0 ? 0 : 1;
    }
}
