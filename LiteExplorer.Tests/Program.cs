using LiteExplorer.Helpers;
using LiteExplorer.MVVM.Models;
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

        // Stage 2 - Section 4.1: File copy policy
        Assert(!customPolicy.CanCopyFile(@"C:\file.docx", @"D:\", out _), "Block copying source from C:");
        Assert(!customPolicy.CanCopyFile(@"D:\file.exe", @"D:\docs", out _), "Block copying disallowed executable file");
        Assert(!customPolicy.CanCopyFile(@"D:\file.docx", @"C:\Windows", out _), "Block copying destination to C:");

        // Stage 2 - Section 4.2: File move policy
        Assert(!customPolicy.CanMoveFile(@"C:\file.docx", @"D:\", out _), "Block moving source from C:");
        Assert(!customPolicy.CanMoveFile(@"D:\file.exe", @"D:\docs", out _), "Block moving disallowed extension");

        // Stage 2 - Section 4.4: Directory creation policy
        Assert(!customPolicy.CanCreateDirectory(@"D:\", "", out _), "Block creating directory with empty name");
        Assert(!customPolicy.CanCreateDirectory(@"D:\", "invalid/name", out _), "Block creating directory with forward slash");
        Assert(!customPolicy.CanCreateDirectory(@"D:\", "invalid\\name", out _), "Block creating directory with backslash");
        Assert(!customPolicy.CanCreateDirectory(@"D:\", "CON", out _), "Block creating directory with reserved DOS name CON");
        Assert(!customPolicy.CanCreateDirectory(@"D:\", "NUL", out _), "Block creating directory with reserved DOS name NUL");
        Assert(!customPolicy.CanCreateDirectory(@"C:\", "NewFolder", out _), "Block creating directory on C:");
        Assert(customPolicy.CanCreateDirectory(@"D:\", "ValidFolder", out _), "Allow creating valid directory name on allowed drive");

        // Stage 2 - Section 4.5: Conflict resolution model
        var conflictResult = new ConflictResolutionResult();
        Assert(conflictResult.Action == ConflictResolutionAction.Skip, "Default conflict resolution action is Skip");
        conflictResult.Action = ConflictResolutionAction.KeepBoth;
        conflictResult.ApplyToAll = true;
        Assert(conflictResult.Action == ConflictResolutionAction.KeepBoth && conflictResult.ApplyToAll, "Conflict resolution model updates correctly");

        // Stage 2 - Section 4.6: Device ejection policy & service
        Assert(!customPolicy.CanEjectDrive(@"C:\", out _), "Block ejecting system drive C:");
        var ejectService = new DriveEjectService(customPolicy);
        bool ejectCResult = ejectService.EjectDrive(@"C:\", out string ejectReason);
        Assert(!ejectCResult && !string.IsNullOrEmpty(ejectReason), "DriveEjectService blocks C: with message");

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



        // Stage 4 - Section 5.1: Breadcrumbs Navigation

        var testVm = new TabContentViewModel(policy);

        Assert(testVm.Breadcrumbs.Count == 1 && testVm.Breadcrumbs[0].Name == "בית", "Home path produces single 'בית' breadcrumb");



        testVm.TabPath = @"D:\Docs\Projects";

        Assert(testVm.Breadcrumbs.Count >= 3, "Nested path produces multiple breadcrumb segments");

        Assert(testVm.Breadcrumbs[0].Name == "בית" && testVm.Breadcrumbs[0].Path == "", "First breadcrumb is always 'בית'");

        Assert(testVm.Breadcrumbs.Last().IsLast, "Last breadcrumb segment has IsLast set to true");

        Assert(testVm.Breadcrumbs.Last().Name == "Projects", "Last breadcrumb segment has correct folder name");



        // Stage 4 - Section 5.2: Search & Quick Filtering

        testVm.FileSystemObjects.Clear();

        testVm.FileSystemObjects.Add(new FileSystemObject { Name = "Document1.docx", Size = 1000 });

        testVm.FileSystemObjects.Add(new FileSystemObject { Name = "Invoice.pdf", Size = 2000 });

        testVm.FileSystemObjects.Add(new FileSystemObject { Name = "Notes.txt", Size = 500 });



        testVm.SearchText = "doc";

        var filteredView = System.Windows.Data.CollectionViewSource.GetDefaultView(testVm.FileSystemObjects);

        var visibleItems = filteredView.Cast<FileSystemObject>().ToList();

        Assert(visibleItems.Count == 1 && visibleItems[0].Name == "Document1.docx", "Filter by 'doc' returns only Document1.docx");



        testVm.SearchText = ".pdf";

        visibleItems = filteredView.Cast<FileSystemObject>().ToList();

        Assert(visibleItems.Count == 1 && visibleItems[0].Name == "Invoice.pdf", "Filter by extension '.pdf' returns only Invoice.pdf");



        testVm.ClearSearchCmd.Execute(null);

        visibleItems = filteredView.Cast<FileSystemObject>().ToList();

        Assert(visibleItems.Count == 3, "Clear search restores full item visibility");



        // Stage 4 - Section 5.3: Column Sorting & Comparer

        var folderItem = new FileSystemObject { Name = "ZebraFolder", IsDirectory = true, Size = 0 };

        var fileItemA = new FileSystemObject { Name = "AlphaFile.txt", IsDirectory = false, Size = 5000, RawLastWriteTime = DateTime.Now.AddDays(-1) };

        var fileItemB = new FileSystemObject { Name = "BetaFile.txt", IsDirectory = false, Size = 1000, RawLastWriteTime = DateTime.Now };



        var nameAscComparer = new FileSystemObjectComparer(SortColumn.Name, System.ComponentModel.ListSortDirection.Ascending);

        Assert(nameAscComparer.Compare(folderItem, fileItemA) < 0, "Comparer keeps directory before file in Ascending order");



        var nameDescComparer = new FileSystemObjectComparer(SortColumn.Name, System.ComponentModel.ListSortDirection.Descending);

        Assert(nameDescComparer.Compare(folderItem, fileItemA) < 0, "Comparer keeps directory before file even in Descending order");



        var sizeAscComparer = new FileSystemObjectComparer(SortColumn.Size, System.ComponentModel.ListSortDirection.Ascending);

        Assert(sizeAscComparer.Compare(fileItemB, fileItemA) < 0, "Sort by size ascending orders smaller file first");



        var dateDescComparer = new FileSystemObjectComparer(SortColumn.LastWriteTime, System.ComponentModel.ListSortDirection.Descending);

        Assert(dateDescComparer.Compare(fileItemB, fileItemA) < 0, "Sort by date descending orders newer file first");



        testVm.SortByColumn("Size");

        Assert(testVm.CurrentSortColumn == SortColumn.Size && testVm.CurrentSortDirection == System.ComponentModel.ListSortDirection.Ascending, "SortByColumn sets Size Ascending");

        testVm.SortByColumn("Size");

        Assert(testVm.CurrentSortColumn == SortColumn.Size && testVm.CurrentSortDirection == System.ComponentModel.ListSortDirection.Descending, "Consecutive SortByColumn toggles direction to Descending");



        // Stage 4 - Section 5.4: Device Notification Service

        bool deviceEventTriggered = false;

        EventHandler handler = (s, e) => { deviceEventTriggered = true; };

        DeviceNotificationService.Instance.DeviceChanged += handler;

        DeviceNotificationService.Instance.NotifyDeviceChanged();

        DeviceNotificationService.Instance.DeviceChanged -= handler;

        Assert(deviceEventTriggered, "DeviceNotificationService triggers DeviceChanged event");



        // Stage 4 - Section 5.5: Quick Preview Model

        var preview = new FilePreviewInfo();

        Assert(preview.Type == PreviewType.None, "Default preview type is None");

        Assert(!string.IsNullOrEmpty(preview.StatusMessage), "Default preview status message is set");



        preview.FileName = "test.txt";

        preview.TextContent = "Sample preview text content";

        preview.Type = PreviewType.Text;

        Assert(preview.Type == PreviewType.Text && preview.TextContent.StartsWith("Sample"), "FilePreviewInfo holds text preview data");



        preview.Clear();

        Assert(preview.Type == PreviewType.None && preview.TextContent == null, "Clear preview resets preview state");



        testVm.Dispose();

        // Stage 5 - Section 4.1 & 4.3: Modernization & CommunityToolkit.Mvvm verification
        Assert(Environment.Version.Major >= 8, ".NET Runtime is version 8 or higher");
        Assert(typeof(LiteExplorer.MVVM.ViewModels.Base.ViewModel).IsSubclassOf(typeof(CommunityToolkit.Mvvm.ComponentModel.ObservableObject)), "ViewModel inherits from CommunityToolkit.Mvvm ObservableObject");

        Console.WriteLine($"\nTest Results: {passed} passed, {failed} failed.");
        return failed == 0 ? 0 : 1;
    }
}
