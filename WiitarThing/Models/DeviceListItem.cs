namespace WiinUSoft.Models;

public sealed class DeviceListItem
{
    public required string DevicePath { get; init; }
    public required string DisplayName { get; init; }
    public DeviceConnectionStatus Status { get; init; }
}
