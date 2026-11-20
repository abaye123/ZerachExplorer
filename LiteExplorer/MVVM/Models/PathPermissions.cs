namespace LiteExplorer.MVVM.Models;

public class PathPermissions
{
    public bool CanList { get; set; } = true;
    public bool CanOpen { get; set; } = true;
    public bool CanCopyFrom { get; set; } = true;
    public bool CanCopyTo { get; set; } = true;
    public bool CanRename { get; set; } = true;
    public bool CanDelete { get; set; } = true;
    public bool CanCreateDirectory { get; set; } = true;

    public static PathPermissions CreateStandard() => new()
    {
        CanList = true,
        CanOpen = true,
        CanCopyFrom = true,
        CanCopyTo = true,
        CanRename = true,
        CanDelete = true,
        CanCreateDirectory = true
    };

    public static PathPermissions CreateReadOnly() => new()
    {
        CanList = true,
        CanOpen = true,
        CanCopyFrom = true,
        CanCopyTo = false,
        CanRename = false,
        CanDelete = false,
        CanCreateDirectory = false
    };

    public static PathPermissions CreateControlled() => new()
    {
        CanList = true,
        CanOpen = true,
        CanCopyFrom = true,
        CanCopyTo = true,
        CanRename = true,
        CanDelete = true,
        CanCreateDirectory = true
    };

    public PathPermissions Clone() => new()
    {
        CanList = CanList,
        CanOpen = CanOpen,
        CanCopyFrom = CanCopyFrom,
        CanCopyTo = CanCopyTo,
        CanRename = CanRename,
        CanDelete = CanDelete,
        CanCreateDirectory = CanCreateDirectory
    };
}
