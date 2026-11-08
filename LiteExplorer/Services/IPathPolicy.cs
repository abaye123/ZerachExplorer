using System.Collections.Generic;
using System.IO;

namespace LiteExplorer.Services;

/// <summary>
/// Central security and policy service for path validation, drive whitelisting,
/// symlink/junction resolution, and file operation authorization.
/// </summary>
public interface IPathPolicy
{
    /// <summary>
    /// File extensions allowed for opening and copying.
    /// </summary>
    IReadOnlyCollection<string> AllowedExtensions { get; }

    /// <summary>
    /// Explicitly whitelisted drive roots (e.g. "D:\").
    /// </summary>
    ISet<string> AllowedDriveRoots { get; }

    /// <summary>
    /// Explicitly whitelisted volume serial numbers (e.g. "12AB-34CD").
    /// </summary>
    ISet<string> AllowedVolumeSerialNumbers { get; }

    /// <summary>
    /// Explicitly whitelisted directories that may reside on non-removable drives.
    /// </summary>
    ISet<string> AllowedExplicitDirectories { get; }

    /// <summary>
    /// Excluded volume labels (e.g. "Otzar Hahochma").
    /// </summary>
    ISet<string> BlockedVolumeLabels { get; }

    /// <summary>
    /// Excluded volume serial numbers.
    /// </summary>
    ISet<string> BlockedVolumeSerialNumbers { get; }

    /// <summary>
    /// Validates whether a drive is permitted according to whitelist policy.
    /// </summary>
    bool IsDriveAllowed(DriveInfo drive, out string reason);

    /// <summary>
    /// Returns all accessible drives that pass the whitelist and exclusion checks.
    /// </summary>
    IEnumerable<DriveInfo> GetAllowedDrives();

    /// <summary>
    /// Validates whether navigation to the given directory path is permitted.
    /// </summary>
    bool CanNavigate(string path, out string reason);

    /// <summary>
    /// Validates whether a directory should be displayed in the file list.
    /// </summary>
    bool CanListDirectory(string directoryPath, out string reason);

    /// <summary>
    /// Validates whether a file should be displayed in the file list.
    /// </summary>
    bool CanListFile(string filePath, out string reason);

    /// <summary>
    /// Validates whether a file is permitted to be opened / launched.
    /// </summary>
    bool CanOpenFile(string filePath, out string reason);

    /// <summary>
    /// Validates whether a file or directory is permitted to be deleted.
    /// </summary>
    bool CanDelete(string path, out string reason);

    /// <summary>
    /// Validates whether renaming a file from oldPath to newPath is permitted.
    /// </summary>
    bool CanRename(string oldPath, string newPath, out string reason);

    /// <summary>
    /// Validates whether copying a file from sourcePath to destinationDirectory is permitted.
    /// </summary>
    bool CanCopyFile(string sourcePath, string destinationDirectory, out string reason);

    /// <summary>
    /// Validates whether moving a file from sourcePath to destinationDirectory is permitted.
    /// </summary>
    bool CanMoveFile(string sourcePath, string destinationDirectory, out string reason);

    /// <summary>
    /// Validates whether copying or moving an entire directory is permitted.
    /// </summary>
    bool CanTransferDirectory(string sourceDirectory, string destinationDirectory, bool isMove, out string reason);

    /// <summary>
    /// Validates whether creating a new directory inside parentDirectory is permitted.
    /// </summary>
    bool CanCreateDirectory(string parentDirectory, string newDirectoryName, out string reason);

    /// <summary>
    /// Validates whether ejecting the specified drive is permitted.
    /// </summary>
    bool CanEjectDrive(string drivePath, out string reason);
}
