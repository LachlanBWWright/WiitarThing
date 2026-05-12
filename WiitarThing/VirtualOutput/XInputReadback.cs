using Shared;

namespace WiinUSoft.VirtualOutput;

internal sealed class XInputReadback : IVirtualControllerReadback
{
    private int _slot;
    private bool _useXbox360GuitarFrets;
    private bool _preferGuitarSlot;
    public string DisplayName => "XInput Readback";

    public Result<Unit, VirtualControllerError> Attach(VirtualControllerIdentity identity)
    {
        if (identity.XInputSlot is not int slot || slot < 1 || slot > 4)
        {
            return Result<Unit, VirtualControllerError>.Err(
                VirtualControllerError.InvalidMapping("XInput readback requires a valid XInput slot (1-4)."));
        }

        _slot = slot;
        _useXbox360GuitarFrets = identity.InstanceId?.Contains("xbox-360-guitar", System.StringComparison.OrdinalIgnoreCase) == true;
        _preferGuitarSlot = _useXbox360GuitarFrets;
        return Result<Unit, VirtualControllerError>.Ok(Unit.Value);
    }

    public Result<ControllerOutputState, VirtualControllerError> ReadState()
    {
        if (_slot <= 0)
        {
            return Result<ControllerOutputState, VirtualControllerError>.Err(
                VirtualControllerError.ConnectionFailed("XInput readback was not attached to a slot."));
        }

        int readSlot = ResolveReadSlot();
        if (!XInputNative.TryGetState(readSlot, out ControllerOutputState state, _useXbox360GuitarFrets))
        {
            return Result<ControllerOutputState, VirtualControllerError>.Err(
                VirtualControllerError.ConnectionFailed($"No virtual XInput device was readable on slot {readSlot}.", readSlot));
        }

        return Result<ControllerOutputState, VirtualControllerError>.Ok(state);
    }

    private int ResolveReadSlot()
    {
        if (!_preferGuitarSlot)
            return _slot;

        if (XInputNative.TryGetSubType(_slot, out byte attachedSubType)
            && XInputNative.IsGuitarSubType(attachedSubType))
        {
            return _slot;
        }

        for (int slot = 1; slot <= 4; slot++)
        {
            if (slot == _slot)
                continue;

            if (XInputNative.TryGetSubType(slot, out byte subType)
                && XInputNative.IsGuitarSubType(subType))
            {
                _slot = slot;
                return slot;
            }
        }

        return _slot;
    }

    public void Dispose()
    {
    }
}
