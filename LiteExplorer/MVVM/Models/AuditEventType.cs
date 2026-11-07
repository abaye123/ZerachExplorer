namespace LiteExplorer.MVVM.Models;

public enum AuditEventType
{
    FileOpened,
    FileCopied,
    FileMoved,
    FileRenamed,
    FileDeleted,
    DirectoryCreated,
    DriveEjected,
    UsbConnected,
    UsbDisconnected,
    SecurityBlocked,
    AdminAccess,
    SessionReset,
    KioskLockdownChanged
}
