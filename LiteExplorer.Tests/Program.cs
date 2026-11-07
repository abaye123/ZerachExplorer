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
        Console.WriteLine("Running LiteExplorer Test Suite (Stage 0 & Stage 1)...");
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

        // 3.1 & 3.2: Case sensitivity & dynamic system root & direct full path navigation
        Assert(!policy.CanNavigate(@"C:\", out _), "3.1 Block uppercase C:\\ navigation");
        Assert(!policy.CanNavigate(@"c:\", out _), "3.1 Block lowercase c:\\ navigation");
        Assert(!policy.CanNavigate(@"c:\windows", out _), "3.1 Block lowercase c:\\windows navigation");
        Assert(!policy.CanNavigate(@"C:\Windows\System32", out _), "3.1 Block C:\\Windows\\System32 navigation");
        Assert(!policy.CanNavigate(PathPolicy.SystemDriveRoot, out _), "3.1 Block dynamic SystemDriveRoot");

        // 3.2: Full path opening tests
        Assert(!policy.CanOpenFile(@"c:\windows\notepad.exe", out _), "3.2 Block opening executable on C:");
        Assert(!policy.CanOpenFile(@"c:\temp\document.docx", out _), "3.2 Block opening document on C:");
        Assert(!policy.CanOpenFile(@"c:\temp\document.pdf", out _), "3.2 Block opening pdf on C:");

        // 3.4: Special paths & UNC Traversal
        Assert(!policy.CanNavigate(@"\\localhost\c$", out _), "3.4 Block UNC network path \\\\localhost\\c$");
        Assert(!policy.CanNavigate(@"\\?\C:\Windows", out _), "3.4 Block NT namespace \\\\?\\C:\\Windows");
        Assert(!policy.CanNavigate(@"\\.\PhysicalDrive0", out _), "3.4 Block device path \\\\.\\PhysicalDrive0");
        Assert(!policy.CanNavigate(@"//server/share", out _), "3.4 Block forward-slash UNC //server/share");
        Assert(!policy.CanOpenFile(@"\\server\share\file.docx", out _), "3.4 Block UNC file opening");
        Assert(!policy.CanNavigate(@"D:\CON", out _), "3.4 Block reserved DOS device CON");
        Assert(!policy.CanNavigate(@"D:\NUL\file.txt", out _), "3.4 Block reserved DOS device NUL in path");

        // 3.5: Rename Path Traversal
        Assert(!policy.CanRename(@"D:\docs\file.docx", @"D:\docs\..\..\evil.docx", out _), "3.5 Block Path Traversal ..\\.. in rename");
        Assert(!policy.CanRename(@"D:\docs\file.docx", @"D:\other\file.docx", out _), "3.5 Block moving file to another directory in rename");
        Assert(!policy.CanRename(@"D:\docs\file.docx", @"D:\docs\file.exe", out _), "3.5 Block extension change in rename");

        // 3.6: Recursive delete & Drive root protection
        Assert(!policy.CanDelete(@"C:\", out _), "3.6 Block delete drive root C:\\");
        Assert(!policy.CanDelete(@"c:\", out _), "3.6 Block delete drive root c:\\");
        Assert(!policy.CanDelete(@"D:\", out _), "3.6 Block delete drive root D:\\");
        Assert(!policy.CanDelete(@"D:", out _), "3.6 Block delete drive root D:");
        Assert(!policy.CanDelete(@"E:\", out _), "3.6 Block delete drive root E:\\");

        // 3.7: Whitelist model
        var customPolicy = new PathPolicy();
        // Disallow Fixed drives by default
        Assert(!customPolicy.AllowedDriveRoots.Contains(@"D:\"), "3.7 Whitelist default does not contain non-removable drives");
        // Add explicit allowed drive
        customPolicy.AllowedDriveRoots.Add(@"D:\");
        Assert(customPolicy.AllowedDriveRoots.Contains(@"D:\"), "3.7 Whitelist accepts explicit drive root");

        // Otzar Hahochma blocked labels
        Assert(customPolicy.BlockedVolumeLabels.Contains("Otzar Hahochma"), "3.7 Blocked labels contains Otzar Hahochma");
        Assert(customPolicy.BlockedVolumeLabels.Contains("אוצר החוכמה"), "3.7 Blocked labels contains Hebrew Otzar Hahochma");

        // Allowed extensions
        Assert(customPolicy.AllowedExtensions.Contains(".docx"), "3.2 Allowed extensions contains .docx");
        Assert(customPolicy.AllowedExtensions.Contains(".pdf"), "3.2 Allowed extensions contains .pdf");
        Assert(customPolicy.AllowedExtensions.Contains(".xlsx"), "3.2 Allowed extensions contains .xlsx");
        Assert(!customPolicy.AllowedExtensions.Contains(".exe"), "3.2 Disallowed extensions does not contain .exe");
        Assert(!customPolicy.AllowedExtensions.Contains(".bat"), "3.2 Disallowed extensions does not contain .bat");
        Assert(!customPolicy.AllowedExtensions.Contains(".ps1"), "3.2 Disallowed extensions does not contain .ps1");

        // ==========================================
        // Stage 1 Tests (Sections 4.2, 4.3, 4.4, 4.5)
        // ==========================================

        // 4.3: CrashLogger writes exception and info messages
        try
        {
            var testEx = new InvalidOperationException("Test unhandled exception for Stage 1 verification");
            CrashLogger.LogException(testEx, "Unit Testing Suite");
            CrashLogger.LogMessage("Unit test verification message", "TEST");

            Assert(File.Exists(CrashLogger.LogFilePath), "4.3 CrashLogger creates log file on disk");
            string logContent = File.ReadAllText(CrashLogger.LogFilePath);
            Assert(logContent.Contains("[CRASH REPORT]"), "4.3 Crash report contains header");
            Assert(logContent.Contains("Test unhandled exception"), "4.3 Crash report contains exception message");
            Assert(logContent.Contains("Unit test verification message"), "4.3 Crash report contains custom message");
        }
        catch (Exception ex)
        {
            Assert(false, "4.3 CrashLogger execution threw unexpected exception", ex.Message);
        }

        // 4.5: Icon cache clearing
        try
        {
            FileManager.ClearCache();
            FolderManager.ClearCache();
            Assert(true, "4.5 Icon caches cleared without error");
        }
        catch (Exception ex)
        {
            Assert(false, "4.5 Icon cache clear threw exception", ex.Message);
        }

        // 4.2 & 4.4: Lazy on-demand folder size calculation and cancellation
        string tempDir = Path.Combine(Path.GetTempPath(), "ZerachExplorerTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            string subDir = Path.Combine(tempDir, "SubDir");
            Directory.CreateDirectory(subDir);

            byte[] bytes1 = new byte[1024]; // 1 KB
            byte[] bytes2 = new byte[2048]; // 2 KB
            File.WriteAllBytes(Path.Combine(tempDir, "file1.bin"), bytes1);
            File.WriteAllBytes(Path.Combine(subDir, "file2.bin"), bytes2);

            long calculatedSize = await TabContentViewModel.CalculateDirectorySizeAsync(tempDir);
            Assert(calculatedSize == 3072, $"4.2 Folder size calculated correctly (expected 3072, got {calculatedSize})");

            // Test cancellation support
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
            Assert(wasCancelled, "4.4 Directory calculation honors cancellation token");
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
