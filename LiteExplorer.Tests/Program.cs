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

        // Stage 3 - Section 4.1 & SecurityHelper: Password hashing & verification
        SecurityHelper.HashPassword("TestSecretPassword!1", out string hash1, out string salt1);
        Assert(!string.IsNullOrEmpty(hash1) && !string.IsNullOrEmpty(salt1), "SecurityHelper creates valid hash and salt");
        Assert(SecurityHelper.VerifyPassword("TestSecretPassword!1", hash1, salt1), "SecurityHelper verifies correct password");
        Assert(!SecurityHelper.VerifyPassword("WrongPassword", hash1, salt1), "SecurityHelper rejects incorrect password");

        // Stage 3 - Section 4.1: ConfigurationService persistence and round-trip
        string tempConfigPath = Path.Combine(Path.GetTempPath(), "LiteExplorer_ConfigTest_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var configService = new ConfigurationService(tempConfigPath);
            var loadedConfig = configService.Load();
            Assert(loadedConfig != null, "ConfigurationService loads default configuration when file does not exist");
            Assert(configService.VerifyPassword("admin123"), "Default admin password verifies successfully");

            loadedConfig.Profile = KioskProfile.ReadOnly;
            loadedConfig.AllowedExtensions = new System.Collections.Generic.List<string> { ".docx", ".pdf" };
            loadedConfig.AllowedDriveRoots = new System.Collections.Generic.List<string> { @"E:\" };
            loadedConfig.Permissions = PathPermissions.CreateReadOnly();

            bool saveSuccess = configService.Save(loadedConfig, out string saveError);
            Assert(saveSuccess && string.IsNullOrEmpty(saveError), "ConfigurationService saves configuration successfully");

            var secondConfigService = new ConfigurationService(tempConfigPath);
            var reloadedConfig = secondConfigService.Load();
            Assert(reloadedConfig.Profile == KioskProfile.ReadOnly, "Configuration persistence retains Profile");
            Assert(reloadedConfig.AllowedExtensions.Contains(".docx") && !reloadedConfig.AllowedExtensions.Contains(".xlsx"), "Configuration persistence retains custom extensions");
            Assert(reloadedConfig.Permissions.CanCopyTo == false && reloadedConfig.Permissions.CanDelete == false, "Configuration persistence retains ReadOnly permissions");

            // Password change
            secondConfigService.ChangePassword("NewAdminPass456");
            Assert(secondConfigService.VerifyPassword("NewAdminPass456"), "ConfigurationService verifies updated password");
            Assert(!secondConfigService.VerifyPassword("admin123"), "ConfigurationService rejects old password after update");
        }
        finally
        {
            if (File.Exists(tempConfigPath))
            {
                try { File.Delete(tempConfigPath); } catch { }
            }
        }

        // Stage 3 - Section 4.3: Read-Only Kiosk Profile policy enforcement
        var readOnlyPolicy = new PathPolicy();
        readOnlyPolicy.AllowedDriveRoots.Add(@"D:\");
        readOnlyPolicy.Profile = KioskProfile.ReadOnly;
        readOnlyPolicy.Permissions = PathPermissions.CreateReadOnly();

        Assert(!readOnlyPolicy.CanDelete(@"D:\docs\file.docx", out _), "ReadOnly profile blocks file deletion");
        Assert(!readOnlyPolicy.CanRename(@"D:\docs\file.docx", @"D:\docs\renamed.docx", out _), "ReadOnly profile blocks file renaming");
        Assert(!readOnlyPolicy.CanCreateDirectory(@"D:\docs", "NewDir", out _), "ReadOnly profile blocks directory creation");
        Assert(!readOnlyPolicy.CanCopyFile(@"D:\docs\file.docx", @"D:\other", out _), "ReadOnly profile blocks copying file (CanCopyTo = false)");
        Assert(!readOnlyPolicy.CanMoveFile(@"D:\docs\file.docx", @"D:\other", out _), "ReadOnly profile blocks moving file");
        Assert(readOnlyPolicy.CanOpenFile(@"D:\docs\file.docx", out _), "ReadOnly profile allows opening permitted document");
        Assert(readOnlyPolicy.CanNavigate(@"D:\docs", out _), "ReadOnly profile allows navigating permitted directory");

        // Stage 3 - Section 4.3: Controlled Workstation Profile policy enforcement
        var controlledPolicy = new PathPolicy();
        controlledPolicy.Profile = KioskProfile.Controlled;
        controlledPolicy.Permissions = PathPermissions.CreateControlled();
        controlledPolicy.AllowedExplicitDirectories.Add(@"D:\PermittedFolder");

        Assert(controlledPolicy.CanNavigate(@"D:\PermittedFolder", out _), "Controlled profile allows navigation within explicit allowed directory");
        Assert(controlledPolicy.CanNavigate(@"D:\PermittedFolder\SubFolder", out _), "Controlled profile allows navigation within nested allowed directory");
        Assert(!controlledPolicy.CanNavigate(@"D:\UnauthorizedFolder", out _), "Controlled profile blocks navigation outside explicit allowed directories");

        // Stage 3 - Section 4.2 & ConfigurationService.ApplyToPolicy
        var configuredPolicy = new PathPolicy();
        var testConfig = new AppConfiguration
        {
            Profile = KioskProfile.Custom,
            Permissions = new PathPermissions
            {
                CanList = false,
                CanOpen = false,
                CanCopyFrom = false,
                CanCopyTo = false,
                CanRename = false,
                CanDelete = false,
                CanCreateDirectory = false
            },
            AllowedExtensions = new System.Collections.Generic.List<string> { ".pdf" }
        };

        string dummyConfigPath = Path.Combine(Path.GetTempPath(), "dummy_config_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var configForPolicy = new ConfigurationService(dummyConfigPath);
            configForPolicy.Save(testConfig, out _);
            configForPolicy.ApplyToPolicy(configuredPolicy);

            Assert(configuredPolicy.Profile == KioskProfile.Custom, "ApplyToPolicy sets custom profile");
            Assert(!configuredPolicy.CanOpenFile(@"D:\file.pdf", out _), "ApplyToPolicy enforces CanOpen=false restriction");
            Assert(!configuredPolicy.CanNavigate(@"D:\", out _), "ApplyToPolicy enforces CanList=false restriction");
            Assert(configuredPolicy.AllowedExtensions.Count == 1 && configuredPolicy.AllowedExtensions.Contains(".pdf"), "ApplyToPolicy updates allowed extensions");
        }
        finally
        {
            if (File.Exists(dummyConfigPath))
            {
                try { File.Delete(dummyConfigPath); } catch { }
            }
        }



        // Stage 4 - Section 5.1: Breadcrumbs Navigation

        using var testVm = new TabContentViewModel(policy, null, null, autoLoad: false);

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



                // Stage 7 - Section 7.1: Audit Logging Service & Entry Models
        string tempAuditPath = Path.Combine(Path.GetTempPath(), $"audit_test_{Guid.NewGuid():N}.log");
        var auditService = new AuditService(tempAuditPath);
        auditService.Log(AuditEventType.FileOpened, @"D:\Document.pdf", "Opened file", true);
        auditService.Log(AuditEventType.SecurityBlocked, @"C:\Windows\System32", "System path blocked", false);

        var recentEntries = auditService.GetRecentEntries(10);
        Assert(recentEntries.Count == 2, "AuditService records and returns logged entries");
        Assert(recentEntries[0].EventType == AuditEventType.SecurityBlocked && !recentEntries[0].Success, "Recent audit entry is newest blocked event");
        Assert(recentEntries[1].EventType == AuditEventType.FileOpened && recentEntries[1].Success, "Prior audit entry is file opened event");

        string formattedEntry = recentEntries[0].ToString();
        var parsedEntry = AuditEntry.Parse(formattedEntry);
        Assert(parsedEntry != null && parsedEntry.EventType == AuditEventType.SecurityBlocked && parsedEntry.Target == @"C:\Windows\System32", "AuditEntry parses formatted log line correctly");

        auditService.ClearLogs();
        Assert(auditService.GetRecentEntries(10).Count == 0, "ClearLogs wipes audit log entries");
        if (File.Exists(tempAuditPath)) { try { File.Delete(tempAuditPath); } catch { } }

        // Stage 7 - Section 7.2: Session Reset & Privacy Service
        bool sessionResetFired = false;
        EventHandler resetHandler = (s, e) => { sessionResetFired = true; };
        SessionService.Instance.SessionReset += resetHandler;
        SessionService.Instance.ResetSession();
        SessionService.Instance.SessionReset -= resetHandler;
        Assert(sessionResetFired, "SessionService raises SessionReset event upon reset");

        // Stage 7 - Section 7.3: Kiosk Controller & Lockdown Exit Interception
        var kiosk = new KioskController();
        Assert(!kiosk.IsKioskModeEnabled, "Default kiosk mode is disabled");
        Assert(!kiosk.PreventAppExit, "Default exit prevention is disabled");

        var cancelArgsDefault = new System.ComponentModel.CancelEventArgs();
        bool canCloseDefault = kiosk.HandleWindowClosing(null, cancelArgsDefault, () => true);
        Assert(canCloseDefault && !cancelArgsDefault.Cancel, "HandleWindowClosing allows close when PreventAppExit is false");

        kiosk.PreventAppExit = true;
        var cancelArgsBlocked = new System.ComponentModel.CancelEventArgs();
        bool canCloseFailedAuth = kiosk.HandleWindowClosing(null, cancelArgsBlocked, () => false);
        Assert(!canCloseFailedAuth && cancelArgsBlocked.Cancel, "HandleWindowClosing blocks exit when authentication fails");

        var cancelArgsAllowed = new System.ComponentModel.CancelEventArgs();
        bool canClosePassedAuth = kiosk.HandleWindowClosing(null, cancelArgsAllowed, () => true);
        Assert(canClosePassedAuth && !kiosk.PreventAppExit, "HandleWindowClosing allows exit and resets PreventAppExit on valid auth");

        // Stage 7 - Section 7.4: ViewMode Toggling & Commands
        Assert(testVm.CurrentViewMode == ViewMode.Details, "Default ViewMode is Details");
        Assert(testVm.IsDetailsView && !testVm.IsTilesView, "IsDetailsView is true and IsTilesView is false by default");

        testVm.ToggleViewModeCmd.Execute(null);
        Assert(testVm.CurrentViewMode == ViewMode.Tiles, "ToggleViewModeCmd switches ViewMode to Tiles");
        Assert(!testVm.IsDetailsView && testVm.IsTilesView, "IsTilesView is true and IsDetailsView is false after toggle");

        testVm.ToggleViewModeCmd.Execute(null);
        Assert(testVm.CurrentViewMode == ViewMode.Details, "Second toggle restores Details ViewMode");

        testVm.Dispose();

        // Stage 5 - Section 4.1 & 4.3: Modernization & CommunityToolkit.Mvvm verification
        Assert(Environment.Version.Major >= 8, ".NET Runtime is version 8 or higher");
        Assert(typeof(LiteExplorer.MVVM.ViewModels.Base.ViewModel).IsSubclassOf(typeof(CommunityToolkit.Mvvm.ComponentModel.ObservableObject)), "ViewModel inherits from CommunityToolkit.Mvvm ObservableObject");

        // Additional Security, Encoding, Sorting, Audit & Configuration Edge-Case Tests
        var advPolicy = new PathPolicy();
        advPolicy.AllowedDriveRoots.Add(@"D:\");
        advPolicy.SetAllowedExtensions(new[] { "TXT", " .Pdf ", "", "  ", ".docx" });
        Assert(advPolicy.AllowedExtensions.Count == 3 && advPolicy.AllowedExtensions.Contains(".txt") && advPolicy.AllowedExtensions.Contains(".pdf"),
            "SetAllowedExtensions normalizes dots, casing, whitespace and ignores blanks");
        Assert(advPolicy.CanOpenFile(@"D:\Docs\REPORT.PDF", out _), "CanOpenFile allows mixed-case permitted extension");
        Assert(!advPolicy.CanOpenFile(@"D:\Docs\report.pdf.exe", out _), "CanOpenFile blocks double extension ending in .exe");
        Assert(!advPolicy.CanNavigate("", out _) && !advPolicy.CanNavigate("   ", out _), "CanNavigate rejects empty or whitespace path");
        Assert(!advPolicy.CanOpenFile("", out _), "CanOpenFile rejects empty path");
        Assert(!advPolicy.CanRename(@"D:\docs\file.docx", @"D:/docs/../../evil.docx", out _), "CanRename blocks forward-slash path traversal");
        Assert(!advPolicy.CanCreateDirectory(@"D:\", "LPT1", out _), "CanCreateDirectory blocks reserved DOS name LPT1");
        Assert(advPolicy.CanCopyFile(@"D:\src\file.docx", @"D:\dst", out _), "CanCopyFile allows permitted extension between allowed drive folders");
        Assert(advPolicy.CanMoveFile(@"D:\src\file.docx", @"D:\dst", out _), "CanMoveFile allows permitted extension between allowed drive folders");

        // UTF-8 Hebrew Breadcrumbs & Search Filtering
        var hebrewVm = new TabContentViewModel(advPolicy);
        hebrewVm.TabPath = @"D:\מסמכים\דוחות\שנתי";
        Assert(hebrewVm.Breadcrumbs.Count >= 4 && hebrewVm.Breadcrumbs[^1].Name == "שנתי" && hebrewVm.Breadcrumbs[^2].Name == "דוחות",
            "Hebrew nested path preserves UTF-8 breadcrumb segments");

        hebrewVm.FileSystemObjects.Clear();
        hebrewVm.FileSystemObjects.Add(new FileSystemObject { Name = "סיכום_שיעור.docx", Size = 1200 });
        hebrewVm.FileSystemObjects.Add(new FileSystemObject { Name = "דוח_שנתי.pdf", Size = 2400 });
        hebrewVm.SearchText = "שיעור";
        var hebrewFiltered = System.Windows.Data.CollectionViewSource.GetDefaultView(hebrewVm.FileSystemObjects).Cast<FileSystemObject>().ToList();
        Assert(hebrewFiltered.Count == 1 && hebrewFiltered[0].Name == "סיכום_שיעור.docx", "Hebrew search filter matches UTF-8 filename");
        hebrewVm.Dispose();

        // Additional Comparer & Permissions Clone Tests
        var dateAscComparer = new FileSystemObjectComparer(SortColumn.LastWriteTime, System.ComponentModel.ListSortDirection.Ascending);
        Assert(dateAscComparer.Compare(fileItemA, fileItemB) < 0, "Sort by date ascending orders older file first");
        var typeAscComparer = new FileSystemObjectComparer(SortColumn.Type, System.ComponentModel.ListSortDirection.Ascending);
        Assert(typeAscComparer.Compare(folderItem, fileItemA) < 0, "Sort by type keeps directory before file");

        var originalPerms = PathPermissions.CreateReadOnly();
        var clonedPerms = originalPerms.Clone();
        clonedPerms.CanDelete = true;
        Assert(!originalPerms.CanDelete && clonedPerms.CanDelete, "PathPermissions.Clone creates an independent copy");

        // SecurityHelper Edge Cases
        SecurityHelper.HashPassword("TestPass123", out string uniqueHash1, out string uniqueSalt1);
        SecurityHelper.HashPassword("TestPass123", out string uniqueHash2, out string uniqueSalt2);
        Assert(uniqueSalt1 != uniqueSalt2 && uniqueHash1 != uniqueHash2, "SecurityHelper generates unique cryptographic salt per hash");
        Assert(!SecurityHelper.VerifyPassword("TestPass123", "", uniqueSalt1), "SecurityHelper rejects empty stored hash");
        Assert(!SecurityHelper.VerifyPassword("TestPass123", "not-valid-base64!!!", uniqueSalt1), "SecurityHelper rejects malformed Base64 hash");

        // AuditService Persistence & Parse Edge Cases
        Assert(AuditEntry.Parse(null!) == null && AuditEntry.Parse("   ") == null && AuditEntry.Parse("corrupt-line")?.Target == string.Empty,
            "AuditEntry.Parse safely handles null, whitespace, and malformed lines without throwing");
        string persistAuditPath = Path.Combine(Path.GetTempPath(), $"audit_persist_{Guid.NewGuid():N}.log");
        try
        {
            var writerAudit = new AuditService(persistAuditPath);
            writerAudit.Log(AuditEventType.UsbConnected, @"E:\", "USB inserted", true);
            writerAudit.Log(AuditEventType.FileCopied, @"E:\a.pdf", "Copied to D:", true);
            writerAudit.Log(AuditEventType.SecurityBlocked, @"C:\Windows", "Blocked", false);
            Assert(writerAudit.GetRecentEntries(2).Count == 2, "AuditService.GetRecentEntries respects requested count limit");

            var readerAudit = new AuditService(persistAuditPath);
            var diskEntries = readerAudit.GetRecentEntries(10);
            Assert(diskEntries.Count == 3 && diskEntries[0].EventType == AuditEventType.SecurityBlocked,
                "New AuditService instance reloads persisted log entries from disk");
        }
        finally
        {
            if (File.Exists(persistAuditPath)) { try { File.Delete(persistAuditPath); } catch { } }
        }

        // Corrupted Configuration Fallback Test
        string corruptConfigPath = Path.Combine(Path.GetTempPath(), $"corrupt_config_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(corruptConfigPath, "{ invalid json !!!");
            var corruptConfigSvc = new ConfigurationService(corruptConfigPath);
            var fallbackCfg = corruptConfigSvc.Load();
            Assert(fallbackCfg != null && fallbackCfg.Profile == KioskProfile.Standard,
                "ConfigurationService falls back to Standard profile on corrupted JSON");
        }
        finally
        {
            if (File.Exists(corruptConfigPath)) { try { File.Delete(corruptConfigPath); } catch { } }
        }

        // Preview Whitelist & Anti-Bypass Tests
        {
            var previewPolicy = new PathPolicy();
            previewPolicy.AllowedDriveRoots.Add(@"D:\");
            previewPolicy.AllowedExplicitDirectories.Add(@"D:\PreviewTest");

            string docxFile = @"D:\PreviewTest\chiddush.docx";
            string pdfFile = @"D:\PreviewTest\shiur.pdf";
            string txtFile = @"D:\PreviewTest\notes.txt";
            string jpgFile = @"D:\PreviewTest\photo.jpg";
            string pngFile = @"D:\PreviewTest\image.png";
            string jsonFile = @"D:\PreviewTest\config.json";
            string exeFile = @"D:\PreviewTest\app.exe";

            Assert(previewPolicy.CanPreviewFile(docxFile, out _), "CanPreviewFile allows whitelisted .docx document");
            Assert(previewPolicy.CanPreviewFile(pdfFile, out _), "CanPreviewFile allows whitelisted .pdf document");
            Assert(previewPolicy.CanPreviewFile(txtFile, out _), "CanPreviewFile allows whitelisted .txt document");
            Assert(!previewPolicy.CanPreviewFile(jpgFile, out _), "CanPreviewFile blocks .jpg image");
            Assert(!previewPolicy.CanPreviewFile(pngFile, out _), "CanPreviewFile blocks .png image");
            Assert(!previewPolicy.CanPreviewFile(jsonFile, out _), "CanPreviewFile blocks .json config file");
            Assert(!previewPolicy.CanPreviewFile(exeFile, out _), "CanPreviewFile blocks .exe executable");
            Assert(!previewPolicy.CanPreviewFile(@"C:\Users\chiddush.docx", out _), "CanPreviewFile blocks .docx on system drive C:");

            // Even if .jpg or .json were added to AllowedExtensions, preview whitelist still blocks them
            previewPolicy.SetAllowedExtensions(new[] { ".docx", ".jpg", ".json" });
            Assert(!previewPolicy.CanPreviewFile(jpgFile, out _), "CanPreviewFile still blocks .jpg even if added to AllowedExtensions");
            Assert(!previewPolicy.CanPreviewFile(jsonFile, out _), "CanPreviewFile still blocks .json even if added to AllowedExtensions");

            // Permissions.CanOpen = false blocks preview
            previewPolicy.Permissions.CanOpen = false;
            Assert(!previewPolicy.CanPreviewFile(docxFile, out _), "CanPreviewFile blocks .docx when CanOpen is false");

            // Alternate Data Stream (ADS) blocked by TryNormalizePath
            Assert(!previewPolicy.TryNormalizePath(docxFile + ":hidden.exe", out _, out _), "TryNormalizePath blocks NTFS Alternate Data Stream (ADS)");
        }

        Console.WriteLine($"\nTest Results: {passed} passed, {failed} failed.");
        return failed == 0 ? 0 : 1;
    }
}
