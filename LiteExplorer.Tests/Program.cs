using LiteExplorer.Services;
using System;
using System.IO;
using System.Linq;

namespace LiteExplorer.Tests;

public class Program
{
    public static int Main()
    {
        Console.WriteLine("Running Security Policy Verification Suite...");
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

        // System root and path navigation
        Assert(!policy.CanNavigate(@"C:\", out _), "Block uppercase C:\\ navigation");
        Assert(!policy.CanNavigate(@"c:\", out _), "Block lowercase c:\\ navigation");
        Assert(!policy.CanNavigate(@"c:\windows", out _), "Block lowercase c:\\windows navigation");
        Assert(!policy.CanNavigate(@"C:\Windows\System32", out _), "Block C:\\Windows\\System32 navigation");
        Assert(!policy.CanNavigate(PathPolicy.SystemDriveRoot, out _), "Block dynamic SystemDriveRoot");

        // File execution authorization
        Assert(!policy.CanOpenFile(@"c:\windows\notepad.exe", out _), "Block opening executable on C:");
        Assert(!policy.CanOpenFile(@"c:\temp\document.docx", out _), "Block opening document on C:");
        Assert(!policy.CanOpenFile(@"c:\temp\document.pdf", out _), "Block opening pdf on C:");

        // Network paths and reserved DOS namespaces
        Assert(!policy.CanNavigate(@"\\localhost\c$", out _), "Block UNC network path \\\\localhost\\c$");
        Assert(!policy.CanNavigate(@"\\?\C:\Windows", out _), "Block NT namespace \\\\?\\C:\\Windows");
        Assert(!policy.CanNavigate(@"\\.\PhysicalDrive0", out _), "Block device path \\\\.\\PhysicalDrive0");
        Assert(!policy.CanNavigate(@"//server/share", out _), "Block forward-slash UNC //server/share");
        Assert(!policy.CanOpenFile(@"\\server\share\file.docx", out _), "Block UNC file opening");
        Assert(!policy.CanNavigate(@"D:\CON", out _), "Block reserved DOS device CON");
        Assert(!policy.CanNavigate(@"D:\NUL\file.txt", out _), "Block reserved DOS device NUL in path");

        // Rename boundary validation and path traversal
        Assert(!policy.CanRename(@"D:\docs\file.docx", @"D:\docs\..\..\evil.docx", out _), "Block Path Traversal ..\\.. in rename");
        Assert(!policy.CanRename(@"D:\docs\file.docx", @"D:\other\file.docx", out _), "Block moving file to another directory in rename");
        Assert(!policy.CanRename(@"D:\docs\file.docx", @"D:\docs\file.exe", out _), "Block extension change in rename");

        // Drive root protection and deletion policy
        Assert(!policy.CanDelete(@"C:\", out _), "Block delete drive root C:\\");
        Assert(!policy.CanDelete(@"c:\", out _), "Block delete drive root c:\\");
        Assert(!policy.CanDelete(@"D:\", out _), "Block delete drive root D:\\");
        Assert(!policy.CanDelete(@"D:", out _), "Block delete drive root D:");
        Assert(!policy.CanDelete(@"E:\", out _), "Block delete drive root E:\\");

        // Whitelist configuration and exclusion checks
        var customPolicy = new PathPolicy();
        Assert(!customPolicy.AllowedDriveRoots.Contains(@"D:\"), "Whitelist default excludes non-removable drives");
        customPolicy.AllowedDriveRoots.Add(@"D:\");
        Assert(customPolicy.AllowedDriveRoots.Contains(@"D:\"), "Whitelist accepts explicit drive root");

        Assert(customPolicy.BlockedVolumeLabels.Contains("Otzar Hahochma"), "Blocked labels contain Otzar Hahochma");
        Assert(customPolicy.BlockedVolumeLabels.Contains("׳׳•׳¦׳¨ ׳”׳—׳•׳›׳׳”"), "Blocked labels contain Hebrew label");

        Assert(customPolicy.AllowedExtensions.Contains(".docx"), "Allowed extensions include .docx");
        Assert(customPolicy.AllowedExtensions.Contains(".pdf"), "Allowed extensions include .pdf");
        Assert(customPolicy.AllowedExtensions.Contains(".xlsx"), "Allowed extensions include .xlsx");
        Assert(!customPolicy.AllowedExtensions.Contains(".exe"), "Disallowed extensions exclude .exe");
        Assert(!customPolicy.AllowedExtensions.Contains(".bat"), "Disallowed extensions exclude .bat");
        Assert(!customPolicy.AllowedExtensions.Contains(".ps1"), "Disallowed extensions exclude .ps1");

        Console.WriteLine($"\nTest Results: {passed} passed, {failed} failed.");
        return failed == 0 ? 0 : 1;
    }
}