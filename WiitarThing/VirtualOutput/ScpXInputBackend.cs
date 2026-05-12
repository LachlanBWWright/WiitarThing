using System;
using NintrollerLib;
using ScpControl;
using Shared;
using WiinUSoft.Holders;

namespace WiinUSoft.VirtualOutput;

internal sealed class ScpXInputBackend : IVirtualControllerBackend
{
    private XBus? _bus;
    private bool _connected;
    private int _slot;
    private ControllerOutputState _lastState = ControllerOutputState.Empty;
    private int _lastRumbleAmount;

    public string DisplayName => "Xbox 360 Gamepad (SCP)";
    public VirtualOutputMode Mode => VirtualOutputMode.ScpXbox360;
    public int LastRumbleAmount => _lastRumbleAmount;

    public Result<Unit, VirtualControllerError> Connect(int slotOrDeviceId, ControllerType sourceType)
    {
        if (sourceType != ControllerType.Guitar)
        {
            return Result<Unit, VirtualControllerError>.Err(
                VirtualControllerError.InvalidMapping("SCP virtual output backend currently supports guitar output only.", slotOrDeviceId));
        }

        if (slotOrDeviceId <= 0 || slotOrDeviceId >= 5)
        {
            return Result<Unit, VirtualControllerError>.Err(
                VirtualControllerError.SlotUnavailable(slotOrDeviceId));
        }

        _bus = XBus.Default;
        if (_bus.State != DsState.Connected)
        {
            return Result<Unit, VirtualControllerError>.Err(
                VirtualControllerError.DriverNotReady(
                    "The SCP virtual bus driver is not available. Install or repair the WiitarThing SCP driver, then restart WiitarThing as administrator.",
                    slotOrDeviceId));
        }

        _bus.Unplug(slotOrDeviceId);
        if (!_bus.Plugin(slotOrDeviceId))
        {
            return Result<Unit, VirtualControllerError>.Err(
                VirtualControllerError.ConnectionFailed($"Failed to create virtual Xbox 360 controller in player slot {slotOrDeviceId}.", slotOrDeviceId));
        }

        XInputHolder.availabe[slotOrDeviceId - 1] = false;
        _slot = slotOrDeviceId;
        _connected = true;
        _lastRumbleAmount = 0;
        _lastState = ControllerOutputState.Empty;
        return Result<Unit, VirtualControllerError>.Ok(Unit.Value);
    }

    public Result<Unit, VirtualControllerError> Update(ControllerOutputState state)
    {
        if (!_connected || _bus == null || _slot <= 0)
        {
            return Result<Unit, VirtualControllerError>.Err(
                VirtualControllerError.ConnectionFailed("Virtual SCP controller is not connected.", _slot > 0 ? _slot : null));
        }

        byte[] rumble = new byte[BusDevice.RumbleSize];
        byte[] report = new byte[BusDevice.ReportSize];
        byte[] parsed = new byte[BusDevice.ReportSize];

        report[0] = (byte)_slot;
        report[1] = 0x02;
        report[10] = 0;
        report[11] = 0;
        report[12] = 0;
        report[13] = 0;

        float rx = ControllerOutputState.ClampSigned(state.Whammy);
        float ry = ControllerOutputState.ClampSigned(state.Tilt);

        report[10] |= (byte)(state.Select ? 1 << 0 : 0);
        report[10] |= (byte)(state.Start ? 1 << 3 : 0);
        report[10] |= (byte)((state.StrumUp) ? 1 << 4 : 0);
        report[10] |= (byte)((state.StrumDown) ? 1 << 5 : 0);
        report[10] |= (byte)((state.DPadRight) ? 1 << 6 : 0);
        report[10] |= (byte)((state.DPadLeft) ? 1 << 7 : 0);

        report[11] |= (byte)(state.Orange ? 1 << 2 : 0);
        report[11] |= (byte)(state.Yellow ? 1 << 4 : 0);
        report[11] |= (byte)(state.Red ? 1 << 5 : 0);
        report[11] |= (byte)(state.Green ? 1 << 6 : 0);
        report[11] |= (byte)(state.Blue ? 1 << 7 : 0);
        report[12] |= (byte)(state.Home ? 1 << 0 : 0);

        report[18] = (byte)((GetRawAxis(rx) >> 0) & 0xFF);
        report[19] = (byte)((GetRawAxis(rx) >> 8) & 0xFF);
        report[20] = (byte)((GetRawAxis(ry) >> 0) & 0xFF);
        report[21] = (byte)((GetRawAxis(ry) >> 8) & 0xFF);

        var parseResult = _bus.TryParse(report, parsed);
        if (parseResult.IsError)
        {
            return Result<Unit, VirtualControllerError>.Err(parseResult.Error);
        }

        _bus.Report(parsed, rumble);
        if (rumble[1] == 0x08)
        {
            int strength = BitConverter.ToInt32(new byte[] { rumble[4], rumble[3], 0x00, 0x00 }, 0);
            _lastRumbleAmount = Math.Max(strength, 0);
        }

        _lastState = state;
        return Result<Unit, VirtualControllerError>.Ok(Unit.Value);
    }

    public Result<Unit, VirtualControllerError> Disconnect()
    {
        if (_bus != null && _slot > 0)
            _bus.Unplug(_slot);

        if (_slot > 0 && _slot < 5)
            XInputHolder.availabe[_slot - 1] = true;

        _slot = 0;
        _connected = false;
        _lastRumbleAmount = 0;
        _lastState = ControllerOutputState.Empty;
        return Result<Unit, VirtualControllerError>.Ok(Unit.Value);
    }

    public Result<VirtualControllerIdentity, VirtualControllerError> GetIdentity()
    {
        if (!_connected || _slot <= 0)
        {
            return Result<VirtualControllerIdentity, VirtualControllerError>.Err(
                VirtualControllerError.ConnectionFailed("Virtual SCP controller is not connected.", _slot > 0 ? _slot : null));
        }

        return Result<VirtualControllerIdentity, VirtualControllerError>.Ok(
            new VirtualControllerIdentity(DisplayName, XInputSlot: _slot, ProductName: "SCP Virtual Xbox 360 Controller"));
    }

    public Result<IVirtualControllerReadback, VirtualControllerError> CreateReadback()
    {
        if (!_connected || _slot <= 0)
        {
            return Result<IVirtualControllerReadback, VirtualControllerError>.Err(
                VirtualControllerError.ConnectionFailed("Cannot create readback before virtual SCP controller is connected.", _slot > 0 ? _slot : null));
        }

        return Result<IVirtualControllerReadback, VirtualControllerError>.Ok(new XInputReadback());
    }

    public void Dispose()
    {
        Disconnect();
    }

    private static int GetRawAxis(double axis)
    {
        if (axis > 1.0)
            return 32767;
        if (axis < -1.0)
            return -32767;

        return (int)(axis * 32767);
    }
}
