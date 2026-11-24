using LiteExplorer.Helpers;
using LiteExplorer.Helpers.WinAPI;
using LiteExplorer.MVVM.Models;
using LiteExplorer.Services;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace LiteExplorer.UnitTests;

public sealed class ServiceTests : IDisposable
{
    private readonly TempFolder temp = new();
    private readonly RecordingAudit audit = new();

    public void Dispose() => temp.Dispose();

    [Fact]
    public void RenameKeepsExtensionAndIsAudited()
    {
        string file = temp.Write("a.txt", "x");

        Assert.True(new FileRenameService(StubPolicy.Create(), audit).Rename(file, "b", out _));

        Assert.True(temp.Exists("b.txt"));
        Assert.Contains(audit.Entries, e => e.Type == AuditEventType.FileRenamed && e.Success);
    }

    [Theory]
    [InlineData("taken")]
    [InlineData("folder")]
    [InlineData(@"sub\b")]
    [InlineData("../b")]
    [InlineData("a:b")]
    public void BadRenameIsRefusedAndAudited(string name)
    {
        string file = temp.Write("a.txt", "x");
        temp.Write("taken.txt", "y");
        Directory.CreateDirectory(temp.At("folder.txt"));

        Assert.False(new FileRenameService(StubPolicy.Create(), audit).Rename(file, name, out var error));

        Assert.NotEmpty(error!);
        Assert.Equal("x", temp.Read("a.txt"));
        Assert.Equal("y", temp.Read("taken.txt"));
        Assert.Contains(audit.Entries, e => e.Type == AuditEventType.FileRenamed && !e.Success);
    }

    [Fact]
    public async Task FolderSizeIncludesSubfoldersAndCanBeCancelled()
    {
        File.WriteAllBytes(temp.At("1.bin"), new byte[1024]);
        Directory.CreateDirectory(temp.At("sub"));
        File.WriteAllBytes(temp.At(@"sub\2.bin"), new byte[2048]);

        Assert.Equal(3072, await FolderInspector.CalculateSizeAsync(temp.Root));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FolderInspector.CalculateSizeAsync(temp.Root, new CancellationToken(true)));
    }

    [Fact]
    public async Task FolderDescriptionCountsHiddenItems()
    {
        File.SetAttributes(temp.Write(@"d\hidden.txt", "x"), FileAttributes.Hidden);
        temp.Write(@"d\sub\y.txt", "y");

        string text = await FolderInspector.DescribeContentsAsync(new[] { temp.At("d") });

        Assert.Contains("2 קבצים, 1 תתי-תיקיות", text);
        Assert.Contains("1 פריטים מוסתרים", text);
        Assert.Empty(await FolderInspector.DescribeContentsAsync(Array.Empty<string>()));
    }

    [Theory]
    [InlineData("windows-1255")]
    [InlineData("utf-8")]
    [InlineData("utf-16")]
    [InlineData("utf-16BE")]
    public async Task PreviewReadsHebrewInCommonEncodings(string name)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        File.WriteAllText(temp.At("t.txt"), "שלום עולם", Encoding.GetEncoding(name));

        Assert.Equal("שלום עולם", await TextPreviewReader.ReadAsync(temp.At("t.txt"), default));
    }

    [Fact]
    public async Task PreviewCutInsideACharacterIsStillUtf8()
    {
        // One ASCII byte shifts the two-byte letters, so the limit falls inside one.
        File.WriteAllText(temp.At("t.txt"), "x" + new string('ש', 70_000));

        string text = await TextPreviewReader.ReadAsync(temp.At("t.txt"), default);

        Assert.Equal("x" + new string('ש', 32_767), text);
    }

    [Fact]
    public void SessionResetClearsClipboardAndTempFiles()
    {
        temp.Write("left.tmp", "x");
        bool cleared = false, raised = false;
        var session = new SessionService(audit, () => cleared = true, temp.Root);
        session.SessionReset += (_, _) => raised = true;

        session.ResetSession();

        Assert.True(cleared && raised);
        Assert.False(temp.Exists("left.tmp"));
        Assert.Contains(audit.Entries, e => e.Type == AuditEventType.SessionReset);
    }

    [Fact]
    public void BusyClipboardDoesNotStopSessionReset()
    {
        bool raised = false;
        var session = new SessionService(audit, () => throw new ExternalException("busy"), temp.Root);
        session.SessionReset += (_, _) => raised = true;

        session.ResetSession();

        Assert.True(raised);
    }

    [Fact]
    public void DeviceChangeIsReported()
    {
        bool raised = false;
        var service = new DeviceNotificationService();
        service.DeviceChanged += (_, _) => raised = true;

        service.NotifyDeviceChanged();

        Assert.True(raised);
    }

    [Theory]
    [InlineData(2, 0x8000, true, true)]
    [InlineData(2, 0x8004, true, false)]
    [InlineData(5, 0x8000, false, false)]
    [InlineData(2, 0x0007, false, false)]
    public void DeviceMessagesReportOnlyVolumes(int deviceType, int code, bool expected, bool expectedArrival)
    {
        IntPtr buffer = Marshal.AllocHGlobal(32);
        try
        {
            Marshal.WriteInt32(buffer, 0, 20);
            Marshal.WriteInt32(buffer, 4, deviceType);
            Marshal.WriteInt32(buffer, 12, (1 << 4) | (1 << 25));

            Assert.Equal(expected, DeviceMessage.TryParse(code, buffer, out bool arrived, out var roots));
            if (expected)
            {
                Assert.Equal(expectedArrival, arrived);
                Assert.Equal(new[] { @"E:\", @"Z:\" }, roots);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Fact]
    public void PasswordHashing()
    {
        SecurityHelper.HashPassword("TestPass123", out var hash, out var salt);
        SecurityHelper.HashPassword("TestPass123", out var otherHash, out var otherSalt);

        Assert.True(SecurityHelper.VerifyPassword("TestPass123", hash, salt));
        Assert.False(SecurityHelper.VerifyPassword("WrongPassword", hash, salt));
        Assert.False(SecurityHelper.VerifyPassword("TestPass123", "", salt));
        Assert.False(SecurityHelper.VerifyPassword("TestPass123", "not-valid-base64!!!", salt));
        Assert.NotEqual(hash, otherHash);
        Assert.NotEqual(salt, otherSalt);
    }

    [Fact]
    public void ConfigurationRoundTrip()
    {
        var service = new ConfigurationService(temp.At("c.json"));
        var config = service.Load();
        config.Profile = KioskProfile.ReadOnly;
        config.AllowedExtensions = new List<string> { ".docx", ".pdf" };
        config.Permissions = PathPermissions.CreateReadOnly();
        config.ShowUsbDialog = true;
        config.LogRetentionDays = 5;
        Assert.True(service.Save(config, out var error));
        Assert.Null(error);

        var reloaded = new ConfigurationService(temp.At("c.json")).Load();

        Assert.Equal(KioskProfile.ReadOnly, reloaded.Profile);
        Assert.Equal(new[] { ".docx", ".pdf" }, reloaded.AllowedExtensions);
        Assert.False(reloaded.Permissions.CanCopyTo || reloaded.Permissions.CanDelete);
        Assert.True(reloaded.ShowUsbDialog);
        Assert.Equal(5, reloaded.LogRetentionDays);
    }

    [Fact]
    public void UsbDialogIsOffByDefault() => Assert.False(new AppConfiguration().ShowUsbDialog);

    [Fact]
    public void ChangedPasswordReplacesTheOldOne()
    {
        var service = new ConfigurationService(temp.At("c.json"));
        service.SetPassword(service.Configuration, "FirstPass123");
        service.SetPassword(service.Configuration, "NewAdminPass456");

        Assert.True(service.VerifyPassword("NewAdminPass456"));
        Assert.False(service.VerifyPassword("FirstPass123"));
    }

    [Fact]
    public void ConfigurationRestrictsPolicy()
    {
        var service = new ConfigurationService(temp.At("c.json"));
        service.Save(new AppConfiguration
        {
            Profile = KioskProfile.Custom,
            Permissions = PathPermissions.CreateDenied(),
            AllowedExtensions = new List<string> { ".pdf" }
        }, out _);
        var policy = new PathPolicy();

        service.ApplyToPolicy(policy);

        Assert.Equal(KioskProfile.Custom, policy.Profile);
        Assert.False(policy.CanOpenFile(@"D:\file.pdf", out _));
        Assert.False(policy.CanNavigate(@"D:\", out _));
        Assert.Equal(new[] { ".pdf" }, policy.AllowedExtensions);
    }

    [Fact]
    public void KioskExitNeedsAuthentication()
    {
        var kiosk = new KioskController();
        Assert.False(kiosk.IsKioskModeEnabled || kiosk.PreventAppExit);

        var args = new CancelEventArgs();
        Assert.True(kiosk.HandleWindowClosing(null, args, () => false));
        Assert.False(args.Cancel);

        kiosk.PreventAppExit = true;
        Assert.False(kiosk.HandleWindowClosing(null, args, () => false));
        Assert.True(args.Cancel);

        args = new CancelEventArgs();
        Assert.True(kiosk.HandleWindowClosing(null, args, () => true));
        Assert.False(args.Cancel || kiosk.PreventAppExit);
    }

    [Theory]
    [InlineData(SortColumn.Name, ListSortDirection.Ascending, "folder,alpha,beta")]
    [InlineData(SortColumn.Name, ListSortDirection.Descending, "folder,beta,alpha")]
    [InlineData(SortColumn.Size, ListSortDirection.Ascending, "folder,beta,alpha")]
    [InlineData(SortColumn.LastWriteTime, ListSortDirection.Ascending, "folder,alpha,beta")]
    [InlineData(SortColumn.LastWriteTime, ListSortDirection.Descending, "folder,beta,alpha")]
    [InlineData(SortColumn.Type, ListSortDirection.Ascending, "folder,alpha,beta")]
    public void SortingKeepsFoldersFirst(SortColumn column, ListSortDirection direction, string expected)
    {
        var items = new List<FileSystemObject>
        {
            new() { Name = "beta", Size = 1000, RawLastWriteTime = new DateTime(2026, 1, 2) },
            new() { Name = "alpha", Size = 5000, RawLastWriteTime = new DateTime(2026, 1, 1) },
            new() { Name = "folder", IsDirectory = true }
        };
        var comparer = new FileSystemObjectComparer(column, direction);

        items.Sort((a, b) => comparer.Compare(a, b));

        Assert.Equal(expected, string.Join(",", items.Select(i => i.Name)));
    }

    [Fact]
    public void PreviewInfoClears()
    {
        var info = new FilePreviewInfo();
        Assert.Equal(PreviewType.None, info.Type);
        Assert.NotEmpty(info.StatusMessage);

        info.Type = PreviewType.Text;
        info.TextContent = "x";
        info.Clear();

        Assert.Equal(PreviewType.None, info.Type);
        Assert.Null(info.TextContent);
    }

    [Fact]
    public void ConflictDefaultsToSkip() => Assert.Equal(ConflictResolutionAction.Skip, new ConflictResolutionResult().Action);

    [Fact]
    public void IconCachesClear()
    {
        FileManager.ClearCache();
        FolderManager.ClearCache();
    }
}
