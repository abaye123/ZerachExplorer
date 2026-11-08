namespace LiteExplorer.MVVM.Models;

public enum ConflictResolutionAction
{
    Skip,
    Replace,
    KeepBoth,
    Cancel
}

public class ConflictResolutionResult
{
    public ConflictResolutionAction Action { get; set; } = ConflictResolutionAction.Skip;
    public bool ApplyToAll { get; set; }
}
