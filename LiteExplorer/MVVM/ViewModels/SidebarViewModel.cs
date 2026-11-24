using LiteExplorer.Helpers;
using LiteExplorer.Helpers.Enums;
using LiteExplorer.MVVM.Models;
using LiteExplorer.Services;
using System.Collections.ObjectModel;
using System.IO;

namespace LiteExplorer.MVVM.ViewModels;

internal sealed class SidebarViewModel
{
    private readonly IPathPolicy policy;
    private int version;

    public SidebarViewModel(IPathPolicy policy)
    {
        this.policy = policy;
    }

    public ObservableCollection<FileSystemObject> QuickAccess { get; } = new();
    public ObservableCollection<FileSystemObject> Drives { get; } = new();

    public async Task RefreshAsync()
    {
        int current = ++version;
        var (folders, drives) = await Task.Run(() => (
            ListFolders().ToList(),
            policy.GetAllowedDrives().Select(DriveDescriber.CreateItem).ToList()));

        if (current != version)
            return;

        Replace(QuickAccess, folders);
        Replace(Drives, drives);
    }

    private IEnumerable<FileSystemObject> ListFolders()
    {
        var candidates = new[]
        {
            ("שולחן העבודה", Environment.GetFolderPath(Environment.SpecialFolder.Desktop)),
            ("מסמכים", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
            ("הורדות", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"))
        };

        return candidates
            .Where(c => Directory.Exists(c.Item2) && policy.CanNavigate(c.Item2, out _))
            .Select(c => new FileSystemObject
            {
                Name = c.Item1,
                Path = c.Item2,
                IsDirectory = true,
                Image = FolderManager.GetImageSource(c.Item2, ItemState.Undefined)
            });
    }

    private static void Replace(ObservableCollection<FileSystemObject> target, List<FileSystemObject> items)
    {
        target.Clear();
        foreach (var item in items)
            target.Add(item);
    }
}
