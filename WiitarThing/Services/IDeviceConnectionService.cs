using NintrollerLib;

namespace WiinUSoft.Services;

public interface IDeviceConnectionService
{
    bool TryEnsureStreamOpen(Nintroller device);
    int? GetFirstAvailablePlayer();
    bool TryConnectToXInput(DeviceControl deviceControl, int playerNumber);
}
