using Shared;
using Shared.Windows;
using System.Collections.Generic;

namespace WiinUSoft.Services;

public sealed class DeviceDiscoveryService : IDeviceDiscoveryService
{
    public Result<List<DeviceInfo>, DeviceDiscoveryError> DiscoverDevices()
    {
        return WinBtStream.TryGetPaths();
    }
}
