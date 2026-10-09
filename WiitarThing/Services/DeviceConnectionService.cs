using NintrollerLib;
using Shared.Windows;
using WiinUSoft.Models;

namespace WiinUSoft.Services;

public sealed class DeviceConnectionService : IDeviceConnectionService
{
    public bool TryEnsureStreamOpen(Nintroller device)
    {
        if (device.DataStream is not WinBtStream stream)
            return false;

        var openResult = stream.TryOpenConnection();
        if (openResult.IsError)
        {
            WiitarDebug.Log(openResult.Error.ToDisplayString(), WiitarDebug.LogLevel.Error);
            return false;
        }

        return stream.CanRead;
    }

    public int? GetFirstAvailablePlayer()
    {
        for (int i = 0; i < 4; i++)
        {
            if (Holders.XInputHolder.availabe.Length > i && Holders.XInputHolder.availabe[i])
                return i + 1;
        }

        return null;
    }

    public bool TryConnectToXInput(IDeviceSession deviceSession, int playerNumber)
    {
        if (deviceSession is not IConnectableDeviceSession connectableDevice)
            return false;

        if (playerNumber < 1 || playerNumber > 4)
            return false;

        if (Holders.XInputHolder.availabe.Length < playerNumber || !Holders.XInputHolder.availabe[playerNumber - 1])
            return false;

        if (!connectableDevice.Controller.Connected && !TryEnsureStreamOpen(connectableDevice.Controller))
            return false;

        return connectableDevice.AssignToXInput(playerNumber).IsOk;
    }
}
