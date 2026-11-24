using System;

namespace ZerachExplorer.Services;

public interface IDeviceNotificationService
{
    event EventHandler? DeviceChanged;
    void NotifyDeviceChanged();

    /// <summary>Raised before ejecting a drive so that open handles can be released.</summary>
    event EventHandler<string>? DriveReleasing;
    event EventHandler<string>? DriveReleaseFinished;
    void NotifyDriveReleasing(string root);
    void NotifyDriveReleaseFinished(string root);
}

public class DeviceNotificationService : IDeviceNotificationService
{
    private static readonly Lazy<DeviceNotificationService> instance = new(() => new DeviceNotificationService());
    public static DeviceNotificationService Instance => instance.Value;

    public event EventHandler? DeviceChanged;
    public event EventHandler<string>? DriveReleasing;
    public event EventHandler<string>? DriveReleaseFinished;

    public void NotifyDriveReleasing(string root) => DriveReleasing?.Invoke(this, root);

    public void NotifyDriveReleaseFinished(string root) => DriveReleaseFinished?.Invoke(this, root);

    public void NotifyDeviceChanged()
    {
        DeviceChanged?.Invoke(this, EventArgs.Empty);
    }
}
