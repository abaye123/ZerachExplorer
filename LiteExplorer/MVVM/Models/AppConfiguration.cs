using System.Collections.Generic;

namespace LiteExplorer.MVVM.Models;

public class AppConfiguration
{
    public KioskProfile Profile { get; set; } = KioskProfile.Standard;
    public string AdminPasswordHash { get; set; } = string.Empty;
    public string AdminPasswordSalt { get; set; } = string.Empty;
    public List<string> AllowedExtensions { get; set; } = new()
    {
        ".doc", ".docx", ".xls", ".xlsx", ".pdf", ".txt", ".csv", ".one", ".onetoc2"
    };
    public List<string> AllowedDriveRoots { get; set; } = new();
    public List<string> AllowedVolumeSerialNumbers { get; set; } = new();
    public List<string> AllowedExplicitDirectories { get; set; } = new();
    public List<string> BlockedVolumeLabels { get; set; } = new()
    {
        "Otzar Hahochma", "Otzar", "אוצר החוכמה"
    };
    public List<string> BlockedVolumeSerialNumbers { get; set; } = new();
    public PathPermissions Permissions { get; set; } = PathPermissions.CreateStandard();
}
