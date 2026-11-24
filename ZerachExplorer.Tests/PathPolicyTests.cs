using System.IO;
using ZerachExplorer.MVVM.Models;
using ZerachExplorer.Services;
using Xunit;

namespace ZerachExplorer.Tests;

/// <summary>Rules that decide by path alone, so D: does not have to exist.</summary>
public class PathPolicyTests
{
    private readonly PathPolicy policy = new();

    public PathPolicyTests()
    {
        policy.AllowedDriveRoots.Add(@"D:\");
    }

    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"c:\")]
    [InlineData(@"c:\windows")]
    [InlineData(@"C:\Windows\System32")]
    [InlineData(@"\\localhost\c$")]
    [InlineData(@"\\?\C:\Windows")]
    [InlineData(@"\\.\PhysicalDrive0")]
    [InlineData("//server/share")]
    [InlineData(@"D:\CON")]
    [InlineData(@"D:\NUL\file.txt")]
    [InlineData("")]
    [InlineData("   ")]
    public void NavigationIsBlocked(string path) => Assert.False(new PathPolicy().CanNavigate(path, out _));

    [Fact]
    public void DynamicSystemDriveIsBlocked() => Assert.False(policy.CanNavigate(PathPolicy.SystemDriveRoot, out _));

    [Theory]
    [InlineData(@"c:\windows\notepad.exe")]
    [InlineData(@"c:\temp\document.docx")]
    [InlineData(@"c:\temp\document.pdf")]
    [InlineData(@"\\server\share\file.docx")]
    [InlineData(@"D:\Docs\report.pdf.exe")]
    [InlineData("")]
    public void OpeningIsBlocked(string path) => Assert.False(policy.CanOpenFile(path, out _));

    [Fact]
    public void ExtensionCaseIsIgnored() => Assert.True(policy.CanOpenFile(@"D:\Docs\REPORT.PDF", out _));

    [Theory]
    [InlineData(@"D:\docs\..\..\evil.docx")]
    [InlineData("D:/docs/../../evil.docx")]
    [InlineData(@"D:\other\file.docx")]
    [InlineData(@"D:\docs\file.exe")]
    public void RenameIsBlocked(string newPath) => Assert.False(policy.CanRename(@"D:\docs\file.docx", newPath, out _));

    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"c:\")]
    [InlineData(@"D:\")]
    [InlineData("D:")]
    [InlineData(@"E:\")]
    public void DeletingADriveIsBlocked(string path) => Assert.False(policy.CanDelete(path, out _));

    [Fact]
    public void DriveWhitelistIsEmptyByDefault() => Assert.Empty(new PathPolicy().AllowedDriveRoots);

    [Theory]
    [InlineData("Otzar Hahochma")]
    [InlineData("אוצר החוכמה")]
    public void OtzarLabelsAreBlocked(string label) => Assert.Contains(label, policy.BlockedVolumeLabels);

    [Theory]
    [InlineData(".docx", true)]
    [InlineData(".pdf", true)]
    [InlineData(".xlsx", true)]
    [InlineData(".exe", false)]
    [InlineData(".bat", false)]
    [InlineData(".ps1", false)]
    public void DefaultExtensions(string extension, bool allowed) => Assert.Equal(allowed, policy.AllowedExtensions.Contains(extension));

    [Theory]
    [InlineData(@"C:\file.docx", @"D:\", false)]
    [InlineData(@"D:\file.exe", @"D:\docs", false)]
    [InlineData(@"D:\file.docx", @"C:\Windows", false)]
    [InlineData(@"D:\src\file.docx", @"D:\dst", true)]
    public void Copying(string source, string target, bool allowed) => Assert.Equal(allowed, policy.CanCopyFile(source, target, out _));

    [Theory]
    [InlineData(@"C:\file.docx", @"D:\", false)]
    [InlineData(@"D:\file.exe", @"D:\docs", false)]
    [InlineData(@"D:\src\file.docx", @"D:\dst", true)]
    public void Moving(string source, string target, bool allowed) => Assert.Equal(allowed, policy.CanMoveFile(source, target, out _));

    [Theory]
    [InlineData(@"D:\", "", false)]
    [InlineData(@"D:\", "invalid/name", false)]
    [InlineData(@"D:\", @"invalid\name", false)]
    [InlineData(@"D:\", "CON", false)]
    [InlineData(@"D:\", "NUL", false)]
    [InlineData(@"D:\", "LPT1", false)]
    [InlineData(@"C:\", "NewFolder", false)]
    [InlineData(@"D:\", "ValidFolder", true)]
    public void CreatingFolders(string parent, string name, bool allowed) => Assert.Equal(allowed, policy.CanCreateDirectory(parent, name, out _));

    [Fact]
    public void EjectingTheSystemDriveIsBlockedWithAMessage()
    {
        Assert.False(policy.CanEjectDrive(@"C:\", out _));
        Assert.False(new DriveEjectService(policy).EjectDrive(@"C:\", out var message));
        Assert.NotEmpty(message);
    }

    [Fact]
    public void ReadOnlyProfileAllowsReadingOnly()
    {
        policy.Profile = KioskProfile.ReadOnly;
        policy.Permissions = PathPermissions.CreateReadOnly();

        Assert.False(policy.CanDelete(@"D:\docs\file.docx", out _));
        Assert.False(policy.CanRename(@"D:\docs\file.docx", @"D:\docs\renamed.docx", out _));
        Assert.False(policy.CanCreateDirectory(@"D:\docs", "NewDir", out _));
        Assert.False(policy.CanCopyFile(@"D:\docs\file.docx", @"D:\other", out _));
        Assert.False(policy.CanMoveFile(@"D:\docs\file.docx", @"D:\other", out _));
        Assert.True(policy.CanOpenFile(@"D:\docs\file.docx", out _));
        Assert.True(policy.CanNavigate(@"D:\docs", out _));
    }

    [Fact]
    public void ControlledProfileAllowsOnlyApprovedFolders()
    {
        var controlled = new PathPolicy { Profile = KioskProfile.Controlled, Permissions = PathPermissions.CreateControlled() };
        controlled.AllowedExplicitDirectories.Add(@"D:\PermittedFolder");

        Assert.True(controlled.CanNavigate(@"D:\PermittedFolder", out _));
        Assert.True(controlled.CanNavigate(@"D:\PermittedFolder\SubFolder", out _));
        Assert.False(controlled.CanNavigate(@"D:\UnauthorizedFolder", out _));
    }

    [Fact]
    public void ExtensionsAreNormalized()
    {
        policy.SetAllowedExtensions(new[] { "TXT", " .Pdf ", "", "  ", ".docx" });

        Assert.Equal(new[] { ".docx", ".pdf", ".txt" }, policy.AllowedExtensions.OrderBy(e => e));
    }

    [Theory]
    [InlineData(@"D:\PreviewTest\chiddush.docx", true)]
    [InlineData(@"D:\PreviewTest\shiur.pdf", true)]
    [InlineData(@"D:\PreviewTest\notes.txt", true)]
    [InlineData(@"D:\PreviewTest\photo.jpg", false)]
    [InlineData(@"D:\PreviewTest\image.png", false)]
    [InlineData(@"D:\PreviewTest\config.json", false)]
    [InlineData(@"D:\PreviewTest\app.exe", false)]
    [InlineData(@"C:\Users\chiddush.docx", false)]
    public void Preview(string path, bool allowed) => Assert.Equal(allowed, policy.CanPreviewFile(path, out _));

    [Theory]
    [InlineData(@"D:\PreviewTest\photo.jpg")]
    [InlineData(@"D:\PreviewTest\config.json")]
    public void PreviewIgnoresWiderExtensionList(string path)
    {
        policy.SetAllowedExtensions(new[] { ".docx", ".jpg", ".json" });

        Assert.False(policy.CanPreviewFile(path, out _));
    }

    [Fact]
    public void PreviewNeedsOpenPermission()
    {
        policy.Permissions.CanOpen = false;

        Assert.False(policy.CanPreviewFile(@"D:\PreviewTest\chiddush.docx", out _));
    }

    [Fact]
    public void DriveLetterAloneMeansItsRoot()
    {
        string previous = Environment.CurrentDirectory;
        Environment.CurrentDirectory = Path.GetTempPath();
        try
        {
            string drive = Path.GetPathRoot(Path.GetTempPath())!.TrimEnd(Path.DirectorySeparatorChar);
            Assert.True(policy.TryNormalizePath(drive, out var root, out _));
            Assert.Equal(drive + "\\", root);
            Assert.False(policy.TryNormalizePath(drive + "folder", out _, out _));
        }
        finally
        {
            Environment.CurrentDirectory = previous;
        }
    }

    [Fact]
    public void AlternateDataStreamIsRejected() => Assert.False(policy.TryNormalizePath(@"D:\a.docx:hidden.exe", out _, out _));

    [Fact]
    public void ClonedPermissionsAreIndependent()
    {
        var original = PathPermissions.CreateReadOnly();
        var clone = original.Clone();
        clone.CanDelete = true;

        Assert.False(original.CanDelete);
    }
}
