using ZerachExplorer.MVVM.Models;
using ZerachExplorer.Services;
using System.IO;
using Xunit;

namespace ZerachExplorer.Tests;

public sealed class FileTransferServiceTests : IDisposable
{
    private readonly TempFolder temp = new();
    private readonly RecordingAudit audit = new();
    private int prompts;

    public FileTransferServiceTests()
    {
        temp.Write(@"src\f\a.txt", "NEW-A");
        temp.Write(@"src\f\sub\b.txt", "NEW-B");
        temp.Write(@"dst\f\a.txt", "OLD-A");
        temp.Write(@"dst\f\sub\b.txt", "OLD-B");
    }

    public void Dispose() => temp.Dispose();

    private FileTransferResult Run(string source, string target, bool move, ConflictResolutionAction action, bool all = false, IPathPolicy? policy = null) =>
        new FileTransferService(policy ?? StubPolicy.Create(), audit).Transfer(new[] { temp.At(source) }, temp.At(target), move, (_, _, _) =>
        {
            prompts++;
            return new ConflictResolutionResult { Action = action, ApplyToAll = all };
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FolderMergeAsksAndSkipKeepsBothSides(bool move)
    {
        Run(@"src\f", "dst", move, ConflictResolutionAction.Skip);

        Assert.Equal(2, prompts);
        Assert.Equal("OLD-A", temp.Read(@"dst\f\a.txt"));
        Assert.Equal("OLD-B", temp.Read(@"dst\f\sub\b.txt"));
        Assert.Equal("NEW-A", temp.Read(@"src\f\a.txt"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplaceOverwritesAndApplyToAllAsksOnce(bool move)
    {
        Run(@"src\f", "dst", move, ConflictResolutionAction.Replace, all: true);

        Assert.Equal(1, prompts);
        Assert.Equal("NEW-A", temp.Read(@"dst\f\a.txt"));
        Assert.Equal("NEW-B", temp.Read(@"dst\f\sub\b.txt"));
        Assert.Equal(!move, temp.Exists(@"src\f"));
        var expected = move ? AuditEventType.FileMoved : AuditEventType.FileCopied;
        Assert.Equal(2, audit.Entries.Count(e => e.Success && e.Type == expected));
        Assert.Empty(Directory.GetFiles(temp.At("dst"), "~zerach*", SearchOption.AllDirectories));
    }

    [Fact]
    public void KeepBothCreatesNumberedFile()
    {
        Run(@"src\f\a.txt", @"dst\f", false, ConflictResolutionAction.KeepBoth);

        Assert.Equal("OLD-A", temp.Read(@"dst\f\a.txt"));
        Assert.Equal("NEW-A", temp.Read(@"dst\f\a (1).txt"));
    }

    [Fact]
    public void CancelStopsTheWholeTransfer()
    {
        var result = Run(@"src\f", "dst", false, ConflictResolutionAction.Cancel);

        Assert.True(result.Cancelled);
        Assert.Equal(1, prompts);
        Assert.Equal("OLD-B", temp.Read(@"dst\f\sub\b.txt"));
    }

    [Fact]
    public void CopyIntoSameFolderCreatesDuplicate()
    {
        Run(@"src\f\a.txt", @"src\f", false, ConflictResolutionAction.Replace);

        Assert.Equal(0, prompts);
        Assert.Equal("NEW-A", temp.Read(@"src\f\a (1).txt"));
    }

    [Fact]
    public void MoveIntoSameFolderChangesNothing()
    {
        Run(@"src\f", "src", true, ConflictResolutionAction.Replace);

        Assert.Equal("NEW-A", temp.Read(@"src\f\a.txt"));
        Assert.False(temp.Exists(@"src\f (1)"));
    }

    [Fact]
    public void DeniedFileIsReportedAndLeftInPlaceOnMove()
    {
        temp.Write(@"src\f\blocked.exe", "X");

        var result = Run(@"src\f", "dst", true, ConflictResolutionAction.Replace, policy: StubPolicy.Create("blocked.exe"));

        Assert.Single(result.Errors);
        Assert.True(temp.Exists(@"src\f\blocked.exe"));
        Assert.False(temp.Exists(@"dst\f\blocked.exe"));
    }
}
