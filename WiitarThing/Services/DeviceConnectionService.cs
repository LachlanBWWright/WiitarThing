using NintrollerLib;

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
            System.Diagnostics.Debug.WriteLine(openResult.Error.ToDisplayString());
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

    public bool TryConnectToXInput(DeviceControl deviceControl, int playerNumber)
    {
        if (playerNumber < 1 || playerNumber > 4)
            return false;

        if (Holders.XInputHolder.availabe.Length < playerNumber || !Holders.XInputHolder.availabe[playerNumber - 1])
            return false;

        if (!deviceControl.Device.Connected && !TryEnsureStreamOpen(deviceControl.Device))
            return false;

        deviceControl.targetXDevice = playerNumber;
        deviceControl.ConnectionState = DeviceState.Connected_XInput;
        deviceControl.Device.BeginReading();
        deviceControl.Device.GetStatus();
        deviceControl.Device.SetPlayerLED(playerNumber);
        return true;
    }
}
