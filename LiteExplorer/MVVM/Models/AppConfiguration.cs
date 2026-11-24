using System.Collections.Generic;

namespace LiteExplorer.MVVM.Models;

public class AppConfiguration
{
    public const int DefaultLogRetentionDays = 30;
    public const int MaximumLogRetentionDays = 365;
    public const int DefaultLogMaxSizeMegabytes = 10;
    public const int MaximumLogMaxSizeMegabytes = 500;

    public int LogRetentionDays { get; set; } = DefaultLogRetentionDays;
    public int LogMaxSizeMegabytes { get; set; } = DefaultLogMaxSizeMegabytes;
    public bool ShowUsbDialog { get; set; }
    public KioskProfile Profile { get; set; } = KioskProfile.Standard;
    public bool KioskModeEnabled { get; set; }
    public bool PreventAppExit { get; set; }
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
    public List<string> BlockedDriveRoots { get; set; } = new();
    public List<string> BlockedVolumeSerialNumbers { get; set; } = new();
    public PathPermissions Permissions { get; set; } = PathPermissions.CreateStandard();
}
