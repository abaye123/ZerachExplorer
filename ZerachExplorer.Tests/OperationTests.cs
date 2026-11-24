using ZerachExplorer.MVVM.Models;
using ZerachExplorer.MVVM.ViewModels;
using ZerachExplorer.Services;
using System.IO;
using Xunit;

namespace ZerachExplorer.Tests;

public sealed class OperationTests : IDisposable
{
    private readonly TempFolder temp = new();
    private readonly RecordingAudit audit = new();

    public OperationTests()
    {
        Directory.CreateDirectory(temp.At("dst"));
    }

    public void Dispose() => temp.Dispose();

    private FileTransferService Service(bool sameVolume = true, long? free = null) =>
        new(StubPolicy.Create(), audit, (_, _) => sameVolume, _ => free);

    private FileTransferResult Run(FileTransferService service, string source, bool move, OperationProgress? progress = null, LowSpacePrompt? lowSpace = null) =>
        service.Transfer(new[] { temp.At(source) }, temp.At("dst"), move, (_, _, _) => new ConflictResolutionResult(), progress, lowSpace);

    private string[] Leftovers() => Directory.GetFiles(temp.Root, "~zerach-*", SearchOption.AllDirectories);

    private void WriteBig(string name, int megabytes)
    {
        using var stream = File.Create(temp.At(name));
        var block = new byte[1024 * 1024];
        for (int i = 0; i < megabytes; i++)
            stream.Write(block);
    }

    [Fact]
    public void ProgressReachesTheMeasuredTotal()
    {
        temp.Write(@"src\a.txt", "12345");
        temp.Write(@"src\sub\b.txt", "1234567");
        using var progress = new OperationProgress();

        Run(Service(), "src", move: false, progress);

        Assert.Equal((2, 2), (progress.TotalItems, progress.DoneItems));
        Assert.Equal((12L, 12L), (progress.TotalBytes, progress.DoneBytes));
    }

    [Fact]
    public void CopyKeepsTimesAttributesAndStreams()
    {
        string source = temp.Write("a.txt", "x");
        File.WriteAllText(source + ":Zone.Identifier", "[ZoneTransfer]");
        File.SetLastWriteTime(source, new DateTime(2020, 1, 2));
        File.SetAttributes(source, FileAttributes.ReadOnly);

        Run(Service(), "a.txt", move: false);

        string copy = temp.At(@"dst\a.txt");
        Assert.Equal(new DateTime(2020, 1, 2), File.GetLastWriteTime(copy));
        Assert.True((File.GetAttributes(copy) & FileAttributes.ReadOnly) != 0);
        Assert.Equal("[ZoneTransfer]", File.ReadAllText(copy + ":Zone.Identifier"));
        File.SetAttributes(copy, FileAttributes.Normal);
        File.SetAttributes(source, FileAttributes.Normal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancelInTheMiddleOfAFileLeavesNothingBehind(bool move)
    {
        WriteBig("big.bin", 20);
        using var progress = new OperationProgress();
        progress.FileBytesReported = bytes =>
        {
            if (bytes > 0)
                progress.Cancel();
        };

        var result = Run(Service(sameVolume: false), "big.bin", move, progress);

        Assert.True(result.Cancelled);
        Assert.True(temp.Exists("big.bin"));
        Assert.False(temp.Exists(@"dst\big.bin"));
        Assert.Empty(Leftovers());
        Assert.DoesNotContain(audit.Entries, e => e.Success);
    }

    [Fact]
    public async Task PauseStopsTheCopyUntilResumed()
    {
        WriteBig("big.bin", 100);
        using var progress = new OperationProgress();
        using var paused = new ManualResetEventSlim();
        progress.FileBytesReported = bytes =>
        {
            if (bytes == 0 || paused.IsSet) return;
            progress.Pause();
            paused.Set();
        };
        var work = Task.Run(() => Run(Service(), "big.bin", move: false, progress));
        await Task.Run(() => paused.Wait());
        long atPause = progress.DoneBytes;
        await Task.Delay(200);
        Assert.Equal(atPause, progress.DoneBytes);
        Assert.False(work.IsCompleted);
        Assert.True(progress.IsPaused);

        progress.Resume();
        await work;
        Assert.Equal(100L * 1024 * 1024, new FileInfo(temp.At(@"dst\big.bin")).Length);
    }

    [Fact]
    public void MoveBetweenVolumesCopiesThenDeletes()
    {
        temp.Write(@"src\a.txt", "x");
        File.SetAttributes(temp.Write(@"src\readonly.txt", "y"), FileAttributes.ReadOnly);

        var result = Run(Service(sameVolume: false), "src", move: true);

        Assert.Empty(result.Errors);
        Assert.False(temp.Exists("src"));
        Assert.Equal("x", temp.Read(@"dst\src\a.txt"));
        File.SetAttributes(temp.At(@"dst\src\readonly.txt"), FileAttributes.Normal);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public void LowSpaceAsksOnlyWhenDataIsCopied(bool sameVolumeMove, int expectedQuestions)
    {
        temp.Write("a.txt", "12345");
        int asked = 0;

        var result = Run(Service(sameVolumeMove, free: 3), "a.txt", move: sameVolumeMove, lowSpace: (needed, free) =>
        {
            asked++;
            Assert.Equal((5L, 3L), (needed, free));
            return false;
        });

        Assert.Equal(expectedQuestions, asked);
        Assert.Equal(expectedQuestions == 1, result.Cancelled);
        Assert.Equal(sameVolumeMove, temp.Exists(@"dst\a.txt"));
    }

    [Fact]
    public void OldPartialFilesAreRemovedAndHidden()
    {
        string old = temp.Write(@"dst\~zerach-0123.tmp", "x");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-3));
        File.SetCreationTimeUtc(old, DateTime.UtcNow.AddDays(-3));
        string recent = temp.Write(@"dst\~zerach-4567.tmp", "x");
        temp.Write("a.txt", "x");

        Run(Service(), "a.txt", move: false);

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
        Assert.True(FileTransferService.IsTemporaryName(Path.GetFileName(recent)));
        Assert.False(FileTransferService.IsTemporaryName("~zerach-notes.txt"));
    }

    [Fact]
    public void PermanentDeletionHandlesReadOnlyAndDoesNotFollowLinks()
    {
        temp.Write(@"outside\keep.txt", "x");
        File.SetAttributes(temp.Write(@"d\sub\readonly.txt", "x"), FileAttributes.ReadOnly);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd", $"/c mklink /J \"{temp.At(@"d\link")}\" \"{temp.At("outside")}\"") { CreateNoWindow = true })!.WaitForExit();
        Assert.True(temp.Exists(@"d\link\keep.txt"));
        using var progress = new OperationProgress();

        Assert.True(new FileDeletionService(StubPolicy.Create(), audit).DeletePermanently(temp.At("d"), out var error, progress), error);

        Assert.False(temp.Exists("d"));
        Assert.True(temp.Exists(@"outside\keep.txt"));
        Assert.Equal(4, progress.DoneItems);
    }

    [Fact]
    public void FolderCreationReportsProblems()
    {
        var service = new FolderCreationService(StubPolicy.Create("secret"), audit);

        Assert.True(service.Create(temp.Root, "new", out _));
        Assert.False(service.Create(temp.Root, "new", out var exists));
        Assert.False(service.Create(temp.At("secret"), "x", out var denied));

        Assert.NotNull(exists);
        Assert.Equal("denied", denied);
        Assert.Equal(new[] { true, false, false }, audit.Entries.Select(e => e.Success));
    }

    [Fact]
    public void QuickOperationsAreNeverShown() => Ui.Run(async () =>
    {
        var operations = new OperationsViewModel(TimeSpan.FromMilliseconds(100));
        bool shown = false;
        operations.ShowRequested += () => shown = true;

        Assert.Equal(5, await operations.RunAsync("x", Array.Empty<string>(), true, _ => 5));
        await Task.Delay(250);

        Assert.False(shown || operations.HasActive || operations.HasVisible);
    });

    [Fact]
    public void FailuresReachTheCallerAndClearTheList() => Ui.Run(async () =>
    {
        var operations = new OperationsViewModel(TimeSpan.Zero);

        await Assert.ThrowsAsync<IOException>(() => operations.RunAsync<int>("x", Array.Empty<string>(), true, _ =>
        {
            Thread.Sleep(100);
            throw new IOException();
        }));

        Assert.False(operations.HasActive || operations.HasVisible);
    });

    [Theory]
    [InlineData(2, "כמה שניות")]
    [InlineData(32, "כ-35 שניות")]
    [InlineData(59.5, "כ-60 שניות")]
    [InlineData(60, "כדקה")]
    [InlineData(61, "כ-2 דקות")]
    [InlineData(3600, "כשעה")]
    [InlineData(7500, "כ-2 שעות ו-5 דקות")]
    public void RemainingTimeIsReadable(double seconds, string expected) => Assert.Equal(expected, FileOperation.Describe(TimeSpan.FromSeconds(seconds)));
}
