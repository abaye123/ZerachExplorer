using ZerachExplorer.MVVM.Models;
using ZerachExplorer.Services;
using System.IO;

namespace ZerachExplorer.MVVM.ViewModels;

internal static class BreadcrumbBuilder
{
    private static readonly char[] Separators = { '\\', '/' };

    public static IReadOnlyList<BreadcrumbItem> Build(string? path)
    {
        var items = new List<BreadcrumbItem> { new() { Name = "בית" } };

        string? root = string.IsNullOrEmpty(path) ? null : Path.GetPathRoot(path);
        if (!string.IsNullOrEmpty(root))
        {
            items.Add(new BreadcrumbItem { Name = DriveDescriber.GetLabel(root), Path = root });

            string accumulated = root;
            foreach (var part in path![root.Length..].Split(Separators, StringSplitOptions.RemoveEmptyEntries))
            {
                accumulated = Path.Combine(accumulated, part);
                items.Add(new BreadcrumbItem { Name = part, Path = accumulated });
            }
        }

        items[^1].IsLast = true;
        return items;
    }
}
