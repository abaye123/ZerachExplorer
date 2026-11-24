using LiteExplorer.Services;

namespace LiteExplorer.MVVM.ViewModels;

/// <summary>The services shared by every explorer tab.</summary>
internal sealed record ExplorerServices(
    IPathPolicy Policy,
    IDialogService Dialogs,
    IClipboardService Clipboard,
    IDirectoryLoader Loader,
    IFileTransferService Transfer,
    IFileDeletionService Deletion,
    IFileRenameService Rename,
    IFolderCreationService Folders,
    IDriveEjectService Eject,
    IAuditService Audit,
    IDeviceNotificationService Devices,
    ISessionService Session,
    OperationsViewModel Operations,
    ViewSettings View);
