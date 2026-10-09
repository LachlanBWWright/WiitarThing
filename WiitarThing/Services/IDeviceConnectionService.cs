using NintrollerLib;
using WiinUSoft.Models;

namespace WiinUSoft.Services;

public interface IDeviceConnectionService
{
    bool TryEnsureStreamOpen(Nintroller device);
    int? GetFirstAvailablePlayer();
    bool TryConnectToXInput(IDeviceSession deviceSession, int playerNumber);
}
