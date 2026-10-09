using NintrollerLib;
using Shared;
using WiinUSoft.Holders;
using WiinUSoft.VirtualOutput;

namespace WiinUSoft.Services;

internal sealed record DeviceStateConnectionResult(
    Holder? Holder,
    bool UsesVirtualOutput,
    VirtualControllerError? Error);

/// <summary>Coordinates the non-visual work required when a device enters a connected state.</summary>
internal sealed class DeviceStateCoordinator
{
    private readonly DeviceConnectionWorkflow _connectionWorkflow;
    private readonly VirtualOutputSession _virtualOutput;
    private readonly System.Func<Nintroller, Property, int, Result<Holder, VirtualControllerError>> _connectHolder;

    public DeviceStateCoordinator(DeviceConnectionWorkflow connectionWorkflow, VirtualOutputSession virtualOutput,
        System.Func<Nintroller, Property, int, Result<Holder, VirtualControllerError>>? connectHolder = null)
    {
        _connectionWorkflow = connectionWorkflow;
        _virtualOutput = virtualOutput;
        _connectHolder = connectHolder ?? ((controller, properties, player) =>
            _connectionWorkflow.ConnectXInput(controller, properties.profile, player));
    }

    public DeviceStateConnectionResult Connect(
        Nintroller controller,
        ControllerType controllerType,
        Property properties,
        int player)
    {
        if (controllerType == ControllerType.Guitar)
        {
            var virtualResult = _virtualOutput.Connect(player, ControllerType.Guitar);
            return new DeviceStateConnectionResult(null, true, virtualResult.IsError ? virtualResult.Error : null);
        }

        var holderResult = _connectHolder(controller, properties, player);
        return new DeviceStateConnectionResult(
            holderResult.IsError ? null : holderResult.Value,
            false,
            holderResult.IsError ? holderResult.Error : null);
    }
}
