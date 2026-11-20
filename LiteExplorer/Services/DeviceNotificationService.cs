using System;

namespace LiteExplorer.Services;

public interface IDeviceNotificationService
{
    event EventHandler DeviceChanged;
    void NotifyDeviceChanged();
}

public class DeviceNotificationService : IDeviceNotificationService
{
    private static readonly Lazy<DeviceNotificationService> instance = new(() => new DeviceNotificationService());
    public static DeviceNotificationService Instance => instance.Value;

    public event EventHandler DeviceChanged;

    public void NotifyDeviceChanged()
    {
        DeviceChanged?.Invoke(this, EventArgs.Empty);
    }
}
