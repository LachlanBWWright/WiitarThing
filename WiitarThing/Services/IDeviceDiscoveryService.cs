using Shared;
using Shared.Windows;
using System.Collections.Generic;

namespace WiinUSoft.Services;

public interface IDeviceDiscoveryService
{
    Result<List<DeviceInfo>, DeviceDiscoveryError> DiscoverDevices();
}
