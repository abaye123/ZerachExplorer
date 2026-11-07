using LiteExplorer.Helpers.WinAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LiteExplorer.Services;

public class PathPolicy : IPathPolicy
{
    private static readonly string[] DefaultAllowedExtensions = new[]
    {
        ".doc", ".docx", ".xls", ".xlsx", ".pdf", ".txt", ".csv", ".one", ".onetoc2"
    };

    // 3.4 DOS reserved device names
    private static readonly string[] ReservedDeviceNames = new[]
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private readonly HashSet<string> allowedExtensions;

    public IReadOnlyCollection<string> AllowedExtensions => allowedExtensions;

    public ISet<string> AllowedDriveRoots { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public ISet<string> AllowedVolumeSerialNumbers { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public ISet<string> AllowedExplicitDirectories { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public ISet<string> BlockedVolumeLabels { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Otzar Hahochma",
        "Otzar",
        "אוצר החוכמה"
    };

    public ISet<string> BlockedVolumeSerialNumbers { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static string SystemDriveRoot
    {
        get
        {
            var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrWhiteSpace(winDir))
            {
                var root = Path.GetPathRoot(winDir);
                if (!string.IsNullOrWhiteSpace(root))
                    return EnsureTrailingSeparator(root);
            }

            var sysDir = Environment.SystemDirectory;
            if (!string.IsNullOrWhiteSpace(sysDir))
            {
                var root = Path.GetPathRoot(sysDir);
                if (!string.IsNullOrWhiteSpace(root))
                    return EnsureTrailingSeparator(root);
            }

            return @"C:\";
        }
    }

    public PathPolicy(IEnumerable<string> customAllowedExtensions = null)
    {
        allowedExtensions = new HashSet<string>(
            customAllowedExtensions ?? DefaultAllowedExtensions,
            StringComparer.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        return path.EndsWith(Path.DirectorySeparatorChar.ToString()) || path.EndsWith(Path.AltDirectorySeparatorChar.ToString())
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    public bool IsDriveAllowed(DriveInfo drive, out string reason)
    {
        reason = null;
        if (drive == null)
        {
            reason = "הכונן אינו קיים.";
            return false;
        }

        if (!drive.IsReady)
        {
            reason = "הכונן אינו מוכן לשימוש (יתכן שאין מדיה בהתקן).";
            return false;
        }

        string driveRoot = EnsureTrailingSeparator(drive.RootDirectory?.FullName ?? drive.Name);

        if (string.Equals(driveRoot, SystemDriveRoot, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(drive.Name, @"C:\", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(driveRoot, @"C:\", StringComparison.OrdinalIgnoreCase))
        {
            reason = "הגישה לכונן המערכת (C:) חסומה מטעמי אבטחה.";
            return false;
        }

        return true;
    }

    public IEnumerable<DriveInfo> GetAllowedDrives()
    {
        DriveInfo[] allDrives;
        try
        {
            allDrives = DriveInfo.GetDrives();
        }
        catch
        {
            yield break;
        }

        foreach (var drive in allDrives)
        {
            if (IsDriveAllowed(drive, out _))
            {
                yield return drive;
            }
        }
    }

    // 3.4 Raw and normalized UNC and special device path validation
    public bool TryNormalizePath(string rawPath, out string normalizedPath, out string reason)
    {
        normalizedPath = null;
        reason = null;

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            reason = "הנתיב המבוקש ריק.";
            return false;
        }

        string trimmed = rawPath.Trim();

        if (trimmed.StartsWith(@"\\") || trimmed.StartsWith("//"))
        {
            reason = "נתיבי רשת (UNC) ונתיבי התקן מיוחדים אינם מורשים במערכת.";
            return false;
        }

        if (trimmed.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || trimmed.Contains('\0'))
        {
            reason = "הנתיב מכיל תווים בלתי חוקיים.";
            return false;
        }

        string full;
        try
        {
            full = Path.GetFullPath(trimmed);
        }
        catch (Exception ex)
        {
            reason = $"נתיב לא תקין: {ex.Message}";
            return false;
        }

        if (full.StartsWith(@"\\") || full.StartsWith("//") ||
            (Uri.TryCreate(full, UriKind.Absolute, out var uri) && uri.IsUnc))
        {
            reason = "נתיבי רשת (UNC) אינם מורשים במערכת.";
            return false;
        }

        var segments = full.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var seg in segments)
        {
            var nameOnly = Path.GetFileNameWithoutExtension(seg);
            if (ReservedDeviceNames.Any(r => string.Equals(r, nameOnly, StringComparison.OrdinalIgnoreCase)))
            {
                reason = "הנתיב כולל שם התקן שמור של מערכת ההפעלה.";
                return false;
            }
        }

        var root = Path.GetPathRoot(full);
        if (string.IsNullOrWhiteSpace(root) || root.Length < 2 || root[1] != ':')
        {
            reason = "הנתיב אינו משויך לכונן חוקי במערכת.";
            return false;
        }

        normalizedPath = full;
        return true;
    }

    private bool IsPathWithinAllowedExplicitDirectory(string fullPath)
    {
        foreach (var explicitDir in AllowedExplicitDirectories)
        {
            string normalizedExplicit = EnsureTrailingSeparator(Path.GetFullPath(explicitDir));
            string normalizedTarget = EnsureTrailingSeparator(fullPath);
            if (normalizedTarget.StartsWith(normalizedExplicit, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private bool ValidateLocation(string normalizedPath, out string reason)
    {
        reason = null;

        var root = Path.GetPathRoot(normalizedPath);
        if (string.IsNullOrWhiteSpace(root))
        {
            reason = "נתיב שגוי: לא נמצא שורש כונן.";
            return false;
        }

        if (string.Equals(root, SystemDriveRoot, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(root, @"C:\", StringComparison.OrdinalIgnoreCase) ||
            root.StartsWith("c:", StringComparison.OrdinalIgnoreCase))
        {
            reason = "הגישה לכונן המערכת (C:) חסומה מטעמי אבטחה.";
            return false;
        }

        if (IsPathWithinAllowedExplicitDirectory(normalizedPath))
        {
            return true;
        }

        DriveInfo drive;
        try
        {
            drive = new DriveInfo(root);
        }
        catch (Exception ex)
        {
            reason = $"לא ניתן לגשת לכונן {root}: {ex.Message}";
            return false;
        }

        return IsDriveAllowed(drive, out reason);
    }

    public bool CanNavigate(string path, out string reason) => throw new NotImplementedException();
    public bool CanListDirectory(string directoryPath, out string reason) => throw new NotImplementedException();
    public bool CanListFile(string filePath, out string reason) => throw new NotImplementedException();
    public bool CanOpenFile(string filePath, out string reason) => throw new NotImplementedException();
    public bool CanDelete(string path, out string reason) => throw new NotImplementedException();
    public bool CanRename(string oldPath, string newPath, out string reason) => throw new NotImplementedException();
}
