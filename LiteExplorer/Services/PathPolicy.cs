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

        try
        {
            if (!string.IsNullOrWhiteSpace(drive.VolumeLabel))
            {
                var label = drive.VolumeLabel.Trim();
                if (BlockedVolumeLabels.Contains(label) ||
                    label.IndexOf("Otzar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.Contains("אוצר החוכמה"))
                {
                    reason = "הגישה לכונני תוכנת 'אוצר החוכמה' חסומה.";
                    return false;
                }
            }
        }
        catch
        {
        }

        try
        {
            if (File.Exists(Path.Combine(driveRoot, "otzar.exe")) ||
                File.Exists(Path.Combine(driveRoot, "Otzar.exe")) ||
                Directory.Exists(Path.Combine(driveRoot, "Otzar")))
            {
                reason = "הגישה לכונני תוכנת 'אוצר החוכמה' חסומה.";
                return false;
            }
        }
        catch
        {
        }

        string serialNumber = Interop.GetVolumeSerialNumber(driveRoot);
        if (!string.IsNullOrEmpty(serialNumber) && BlockedVolumeSerialNumbers.Contains(serialNumber))
        {
            reason = "הגישה לכונן זה נחסמה לפי מזהה אמצעי האחסון.";
            return false;
        }

        if (drive.DriveType == DriveType.Removable)
        {
            return true;
        }

        if (AllowedDriveRoots.Contains(driveRoot) ||
            AllowedDriveRoots.Contains(drive.Name) ||
            (!string.IsNullOrEmpty(serialNumber) && AllowedVolumeSerialNumbers.Contains(serialNumber)))
        {
            return true;
        }

        reason = "הכונן אינו מורשה. המערכת מאפשרת גישה לכוננים נשלפים (USB) בלבד.";
        return false;
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

    private bool ValidateReparsePoint(string path, out string finalResolvedPath, out string reason)
    {
        finalResolvedPath = path;
        reason = null;

        string canonicalPath = Interop.GetFinalPath(path);
        if (!string.IsNullOrWhiteSpace(canonicalPath) &&
            !string.Equals(canonicalPath, path, StringComparison.OrdinalIgnoreCase))
        {
            if (!TryNormalizePath(canonicalPath, out string normalizedCanonical, out reason))
            {
                reason = $"הקישור מצביע ליעד בלתי חוקי: {reason}";
                return false;
            }

            if (!ValidateLocation(normalizedCanonical, out reason))
            {
                reason = $"הקישור (Junction / Symlink) מצביע ליעד חסום: {reason}";
                return false;
            }

            finalResolvedPath = normalizedCanonical;
        }

        try
        {
            FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
            if ((info.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                var target = info.ResolveLinkTarget(true);
                if (target == null)
                {
                    reason = "הקישור (Symlink) שבור או שאינו מצביע ליעד תקף.";
                    return false;
                }

                if (!TryNormalizePath(target.FullName, out string targetNormalized, out reason))
                {
                    reason = $"יעד הקישור אינו חוקי: {reason}";
                    return false;
                }

                if (!ValidateLocation(targetNormalized, out reason))
                {
                    reason = $"הקישור מצביע למיקום חסום במערכת: {reason}";
                    return false;
                }

                finalResolvedPath = targetNormalized;
            }
        }
        catch (Exception ex)
        {
            reason = $"שגיאה בבדיקת יעד הקישור: {ex.Message}";
            return false;
        }

        return true;
    }

    private static bool HasHiddenOrSystemAttribute(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var attr = File.GetAttributes(path);
                return (attr & (FileAttributes.Hidden | FileAttributes.System)) != 0;
            }
            if (Directory.Exists(path))
            {
                var attr = File.GetAttributes(path);
                return (attr & (FileAttributes.Hidden | FileAttributes.System)) != 0;
            }
        }
        catch
        {
            return true;
        }
        return false;
    }

    private static bool HasHiddenOrSystemAncestor(string path)
    {
        try
        {
            var dir = new DirectoryInfo(path);
            while (dir != null && dir.Parent != null)
            {
                if ((dir.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                {
                    return true;
                }
                dir = dir.Parent;
            }
        }
        catch
        {
            return true;
        }
        return false;
    }

    public bool CanNavigate(string path, out string reason)
    {
        if (!TryNormalizePath(path, out string fullPath, out reason))
            return false;

        if (!ValidateLocation(fullPath, out reason))
            return false;

        if (!ValidateReparsePoint(fullPath, out string resolvedPath, out reason))
            return false;

        if (!string.Equals(fullPath, resolvedPath, StringComparison.OrdinalIgnoreCase))
        {
            if (!ValidateLocation(resolvedPath, out reason))
                return false;
        }

        if (HasHiddenOrSystemAttribute(resolvedPath) || HasHiddenOrSystemAncestor(resolvedPath))
        {
            reason = "הגישה לתיקיות מוסתרות או תיקיות מערכת חסומה.";
            return false;
        }

        return true;
    }

    public bool CanListDirectory(string directoryPath, out string reason)
    {
        reason = null;
        try
        {
            var di = new DirectoryInfo(directoryPath);
            if ((di.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
            {
                reason = "תיקייה מוסתרת.";
                return false;
            }

            if ((di.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                if (!ValidateReparsePoint(directoryPath, out _, out reason))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            reason = ex.Message;
            return false;
        }
    }

    public bool CanListFile(string filePath, out string reason)
    {
        reason = null;
        try
        {
            var fi = new FileInfo(filePath);
            if ((fi.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
            {
                reason = "קובץ מוסתר או קובץ מערכת.";
                return false;
            }

            if ((fi.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                if (!ValidateReparsePoint(filePath, out _, out reason))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            reason = ex.Message;
            return false;
        }
    }

    public bool CanOpenFile(string filePath, out string reason)
    {
        if (!TryNormalizePath(filePath, out string fullPath, out reason))
            return false;

        if (!ValidateLocation(fullPath, out reason))
            return false;

        if (!ValidateReparsePoint(fullPath, out string resolvedPath, out reason))
            return false;

        if (!string.Equals(fullPath, resolvedPath, StringComparison.OrdinalIgnoreCase))
        {
            if (!ValidateLocation(resolvedPath, out reason))
                return false;
        }

        if (HasHiddenOrSystemAttribute(resolvedPath) || HasHiddenOrSystemAncestor(resolvedPath))
        {
            reason = "חל איסור על פתיחת קובצי מערכת או קבצים מוסתרים.";
            return false;
        }

        string ext = Path.GetExtension(resolvedPath)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
        {
            reason = $"סוג הקובץ '{ext}' אינו מאושר לפתיחה במערכת.";
            return false;
        }

        return true;
    }

    // 3.5 Validation for Rename
    public bool CanRename(string oldPath, string newPath, out string reason)
    {
        if (!TryNormalizePath(oldPath, out string fullOld, out reason))
            return false;

        if (!TryNormalizePath(newPath, out string fullNew, out reason))
            return false;

        if (!ValidateLocation(fullOld, out reason))
            return false;

        if (!ValidateLocation(fullNew, out reason))
            return false;

        var oldDir = Path.GetDirectoryName(fullOld);
        var newDir = Path.GetDirectoryName(fullNew);
        if (!string.Equals(oldDir, newDir, StringComparison.OrdinalIgnoreCase))
        {
            reason = "ניסיון לשינוי מיקום הקובץ (Path Traversal) נחסם.";
            return false;
        }

        var oldExt = Path.GetExtension(fullOld);
        var newExt = Path.GetExtension(fullNew);
        if (!string.Equals(oldExt, newExt, StringComparison.OrdinalIgnoreCase))
        {
            reason = "חל איסור על שינוי סיומת הקובץ.";
            return false;
        }

        return true;
    }

    public bool CanDelete(string path, out string reason) => throw new NotImplementedException();
}
