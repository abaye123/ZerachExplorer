using LiteExplorer.Helpers;
using LiteExplorer.MVVM.Models;
using LiteExplorer.MVVM.ViewModels;
using LiteExplorer.Services;
using System.ComponentModel;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using Xunit;

namespace LiteExplorer.UnitTests;

public sealed class TabTests : IDisposable
{
    private readonly TempFolder temp = new();
    private readonly FakeDialogs dialogs = new();
    private readonly FakeClipboard clipboard = new();
    private readonly RecordingAudit audit = new();
    private IPathPolicy policy = StubPolicy.Create();

    public void Dispose() => temp.Dispose();

    private TabContentViewModel Create()
    {
        return new TabContentViewModel(new ExplorerServices(policy, dialogs, clipboard, new DirectoryLoader(policy),
            new FileTransferService(policy, audit), new FileDeletionService(policy, audit), new FileRenameService(policy, audit),
            new DriveEjectService(policy), audit, new DeviceNotificationService(),
            new SessionService(audit, () => { }, temp.At("none"))));
    }

    private async Task<TabContentViewModel> OpenRoot()
    {
        var tab = Create();
        Assert.True(tab.Open(temp.Root));
        await Ui.Until(() => !tab.IsLoading);
        return tab;
    }

    private static string[] Names(TabContentViewModel tab) =>
        CollectionViewSource.GetDefaultView(tab.FileSystemObjects).Cast<FileSystemObject>().Select(i => i.Name).ToArray();

    [Fact]
    public void LoadsFoldersFirst() => Ui.Run(async () =>
    {
        temp.Write("a.txt", "x");
        temp.Write(@"zebra\b.txt", "x");

        using var tab = await OpenRoot();

        Assert.Equal(new[] { "zebra", "a.txt" }, Names(tab));
        Assert.Equal(temp.Root, tab.TabPath);
    });

    [Fact]
    public void TypingAnAddressDoesNotMoveTheTab() => Ui.Run(async () =>
    {
        temp.Write(@"sub\b.txt", "x");
        using var tab = await OpenRoot();

        tab.ToggleAddressBarEditCmd.Execute(null);
        Assert.Equal(temp.Root, tab.AddressText);
        tab.AddressText = temp.At("missing");
        Assert.Equal(temp.Root, tab.TabPath);

        tab.CommitAddressCmd.Execute(null);
        Assert.Equal(temp.Root, tab.TabPath);
        Assert.True(tab.IsAddressBarEditing);
        Assert.Single(dialogs.Messages);

        tab.AddressText = $"\"{temp.At("sub")}\"";
        tab.CommitAddressCmd.Execute(null);
        await Ui.Until(() => !tab.IsLoading);
        Assert.Equal(temp.At("sub"), tab.TabPath);
        Assert.False(tab.IsAddressBarEditing);
    });

    [Fact]
    public void RefreshKeepsItemsAndUpdatesChanges() => Ui.Run(async () =>
    {
        temp.Write("stays.txt", "x");
        temp.Write("grows.txt", "x");
        temp.Write("gone.txt", "x");
        using var tab = await OpenRoot();
        var stays = tab.FileSystemObjects.Single(i => i.Name == "stays.txt");
        var grows = tab.FileSystemObjects.Single(i => i.Name == "grows.txt");
        var list = new ListBox { ItemsSource = tab.FileSystemObjects, SelectionMode = SelectionMode.Extended };
        list.SelectedItems.Add(stays);
        list.SelectedItems.Add(grows);

        temp.Write("grows.txt", "longer");
        temp.Write("added.txt", "x");
        File.Delete(temp.At("gone.txt"));
        tab.Reload();
        await Ui.Until(() => !tab.IsLoading);

        Assert.Equal(new[] { "added.txt", "grows.txt", "stays.txt" }, Names(tab));
        Assert.Contains(stays, tab.FileSystemObjects);
        Assert.Equal(6, grows.Size);
        Assert.Equal(2, list.SelectedItems.Count);
    });

    [Fact]
    public void WatcherPicksUpNewFiles() => Ui.Run(async () =>
    {
        using var tab = await OpenRoot();

        temp.Write("new.txt", "x");

        await Ui.Until(() => Names(tab).Contains("new.txt"));
    });

    [Fact]
    public void RefreshKeepsCalculatedFolderSize() => Ui.Run(async () =>
    {
        temp.Write(@"d\x.txt", "12345");
        using var tab = await OpenRoot();
        tab.CalculateFolderSizeCmd.Execute(tab.FileSystemObjects[0]);
        await Ui.Until(() => tab.FileSystemObjects[0].Size == 5);

        tab.Reload();
        await Ui.Until(() => !tab.IsLoading);

        Assert.Equal(5, tab.FileSystemObjects[0].Size);
    });

    [Fact]
    public void RemovedFolderReturnsHome() => Ui.Run(async () =>
    {
        temp.Write(@"d\x.txt", "x");
        using var tab = new TabContentViewModel(new ExplorerServices(policy, dialogs, clipboard, new FailingLoader(), null!, null!, null!, null!,
            audit, new DeviceNotificationService(), new SessionService(audit, () => { }, temp.At("none"))));

        tab.Open(temp.At("d"));
        await Ui.Until(() => dialogs.Messages.Count > 0);

        Assert.Null(tab.TabPath);
    });

    [Fact]
    public void BlockedFolderIsAuditedAndNotOpened() => Ui.Run(async () =>
    {
        temp.Write(@"secret\x.txt", "x");
        policy = StubPolicy.Create("secret");
        using var tab = await OpenRoot();

        Assert.False(tab.Open(temp.At("secret")));

        Assert.Equal(temp.Root, tab.TabPath);
        Assert.Contains(audit.Entries, e => e.Type == AuditEventType.SecurityBlocked && !e.Success);
    });

    [Fact]
    public void DeleteActsOnTheWholeSelection() => Ui.Run(async () =>
    {
        temp.Write("a.txt", "x");
        temp.Write("b.txt", "x");
        using var tab = await OpenRoot();
        tab.SetSelectedItems(tab.FileSystemObjects);
        tab.CurrentFileSystemObject = tab.FileSystemObjects[0];

        tab.DeleteCmd.Execute(null);
        await Ui.Until(() => dialogs.Messages.Count > 0);

        Assert.Contains("2 פריטים", dialogs.Messages[0]);
        Assert.True(temp.Exists("a.txt") && temp.Exists("b.txt"));
    });

    [Fact]
    public void RenameAndNewFolderAreAudited() => Ui.Run(async () =>
    {
        temp.Write("a.txt", "x");
        using var tab = await OpenRoot();
        tab.CurrentFileSystemObject = tab.FileSystemObjects[0];
        dialogs.TextAnswer = "renamed";
        dialogs.FolderAnswer = "created";

        tab.RenameCmd.Execute(null);
        tab.CreateDirectoryCmd.Execute(null);
        await Ui.Until(() => Names(tab).Contains("created") && Names(tab).Contains("renamed.txt"));

        Assert.Contains(audit.Entries, e => e.Type == AuditEventType.FileRenamed && e.Success);
        Assert.Contains(audit.Entries, e => e.Type == AuditEventType.DirectoryCreated && e.Success);
    });

    [Fact]
    public void CutAndPasteMovesAndEmptiesClipboard() => Ui.Run(async () =>
    {
        temp.Write("a.txt", "x");
        Directory.CreateDirectory(temp.At("target"));
        using var tab = await OpenRoot();

        tab.CutCmd.Execute(temp.At("a.txt"));
        Assert.True(clipboard.IsCut);
        tab.Open(temp.At("target"));
        tab.PasteCmd.Execute(null);
        await Ui.Until(() => !clipboard.HasFiles() && Names(tab).Contains("a.txt"));

        Assert.False(temp.Exists("a.txt"));
    });

    [Fact]
    public void PreviewShowsTheLatestSelection() => Ui.Run(async () =>
    {
        temp.Write("a.txt", new string('a', 60_000));
        temp.Write("b.txt", "second");
        using var tab = await OpenRoot();
        tab.IsPreviewPaneVisible = true;

        for (int i = 0; i < 20; i++)
        {
            tab.CurrentFileSystemObject = tab.FileSystemObjects.Single(f => f.Name == "a.txt");
            tab.CurrentFileSystemObject = tab.FileSystemObjects.Single(f => f.Name == "b.txt");
        }
        await Ui.Until(() => tab.PreviewInfo.TextContent != null);
        await Task.Delay(100);

        Assert.Equal("second", tab.PreviewInfo.TextContent);
        Assert.Equal("b.txt", tab.PreviewInfo.FileName);
    });

    [Theory]
    [InlineData(null, "בית")]
    [InlineData(@"D:\Docs\Projects", "Projects")]
    [InlineData(@"D:\מסמכים\דוחות\שנתי", "שנתי")]
    public void Breadcrumbs(string? path, string last) => Ui.Run(() =>
    {
        using var tab = Create();
        tab.TabPath = path;

        Assert.Equal("בית", tab.Breadcrumbs[0].Name);
        Assert.Equal(string.Empty, tab.Breadcrumbs[0].Path);
        Assert.Equal(last, tab.Breadcrumbs[^1].Name);
        Assert.Equal(path ?? string.Empty, tab.Breadcrumbs[^1].Path);
        Assert.Single(tab.Breadcrumbs, b => b.IsLast);
        Assert.True(tab.Breadcrumbs[^1].IsLast);
        return Task.CompletedTask;
    });

    [Theory]
    [InlineData("doc", "Document1.docx")]
    [InlineData(".pdf", "Invoice.pdf")]
    [InlineData("שיעור", "סיכום_שיעור.docx")]
    public void SearchFilters(string query, string expected) => Ui.Run(() =>
    {
        using var tab = Create();
        foreach (var name in new[] { "Document1.docx", "Invoice.pdf", "Notes.txt", "סיכום_שיעור.docx" })
            tab.FileSystemObjects.Add(new FileSystemObject { Name = name });

        tab.SearchText = query;
        Assert.Contains(expected, Names(tab));
        Assert.DoesNotContain("Notes.txt", Names(tab));

        tab.ClearSearchCmd.Execute(null);
        Assert.Equal(4, Names(tab).Length);
        return Task.CompletedTask;
    });

    [Fact]
    public void SortingTogglesDirectionAndViewModeToggles() => Ui.Run(() =>
    {
        using var tab = Create();
        tab.SortByColumn("Size");
        Assert.Equal((SortColumn.Size, ListSortDirection.Ascending), (tab.CurrentSortColumn, tab.CurrentSortDirection));
        tab.SortByColumn("Size");
        Assert.Equal(ListSortDirection.Descending, tab.CurrentSortDirection);
        tab.SortByColumn("Name");
        Assert.Equal((SortColumn.Name, ListSortDirection.Ascending), (tab.CurrentSortColumn, tab.CurrentSortDirection));

        Assert.True(tab.IsDetailsView && !tab.IsTilesView);
        tab.ToggleViewModeCmd.Execute(null);
        Assert.True(tab.IsTilesView && !tab.IsDetailsView);
        tab.ToggleViewModeCmd.Execute(null);
        Assert.True(tab.IsDetailsView);
        return Task.CompletedTask;
    });

    [Fact]
    public void BackAndForwardFollowHistory() => Ui.Run(async () =>
    {
        temp.Write(@"a\x.txt", "x");
        temp.Write(@"b\x.txt", "x");
        using var tab = await OpenRoot();
        tab.Open(temp.At("a"));
        tab.Open(temp.At("b"));

        tab.BackCmd.Execute(null);
        tab.BackCmd.Execute(null);
        Assert.Equal(temp.Root, tab.TabPath);
        tab.ForwardCmd.Execute(null);
        Assert.Equal(temp.At("a"), tab.TabPath);
        tab.UpCmd.Execute(null);
        Assert.Equal(temp.Root, tab.TabPath);
        Assert.False(tab.ForwardCmd.CanExecute(null));
        tab.BackCmd.Execute(null);
        Assert.Equal(temp.At("a"), tab.TabPath);
        await Ui.Until(() => !tab.IsLoading);
    });

    [Fact]
    public void TabsCloseCycleAndOpenInBackground() => Ui.Run(async () =>
    {
        temp.Write(@"a\x.txt", "x");
        var main = new MainWindowViewModel(Create);
        var first = main.CurrentTab!;

        first.OpenInNewTabCmd.Execute(temp.At("a"));
        Assert.Same(first, main.CurrentTab);
        Assert.Equal(temp.At("a"), main.Tabs[1].TabPath);

        main.AddTabCmd.Execute(null);
        Assert.Same(main.Tabs[2], main.CurrentTab);
        main.CycleTab(1);
        Assert.Same(first, main.CurrentTab);
        main.CycleTab(-1);
        Assert.Same(main.Tabs[2], main.CurrentTab);
        main.CloseTabCmd.Execute(main.Tabs[1]);
        Assert.Equal(2, main.Tabs.Count);
        Assert.Same(main.Tabs[1], main.CurrentTab);
        main.CloseOtherTabsCmd.Execute(first);
        Assert.Same(first, Assert.Single(main.Tabs));
        await Ui.Until(() => !first.IsLoading);
        first.Dispose();
    });

    private sealed class FailingLoader : IDirectoryLoader
    {
        public async IAsyncEnumerable<DirectoryBatch> LoadAsync(string? path, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            await Task.Yield();
            if (path != null)
                throw new DirectoryNotFoundException(path);
            yield break;
        }
    }
}
