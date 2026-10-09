using System;
using NintrollerLib;
using Shared;
using WiinUSoft;
using WiinUSoft.VirtualOutput;

namespace WiinUSoft.Models;

/// <summary>
/// Application-facing device contract used by view-models. Views consume this
/// contract for display state and never own the hardware transport.
/// </summary>
public interface IDeviceSession : IDisposable
{
    event EventHandler<DeviceSessionStateChangedEventArgs>? StateChanged;
    event EventHandler<DeviceSessionConnectionLostEventArgs>? ConnectionLost;
    event EventHandler<DeviceSessionConnectionFailedEventArgs>? ConnectionFailed;
    event EventHandler? BatteryChanged;
    event EventHandler? PreviewChanged;
    event EventHandler<string>? InlineOutputError;

    string DevicePath { get; }
    string DisplayName { get; set; }
    bool Connected { get; }
    DeviceState ConnectionState { get; }
    int AutoConnectSlot { get; set; }
    ControllerType DeviceType { get; }
    bool IsBatteryLow { get; }
    GuitarPreviewState LastPreviewState { get; }

    void Disconnect();
    void Refresh();
    void SetControllerType(ControllerType controllerType);
    System.Threading.Tasks.Task IdentifyAsync(bool wasConnected);
    bool TryReadVirtualOutput(out GuitarPreviewState state, out string? error);
    Result<Unit, VirtualControllerError> ReconnectVirtualOutput();
}

public interface IConnectableDeviceSession : IDeviceSession
{
    Nintroller Controller { get; }
    Result<Unit, VirtualControllerError> AssignToXInput(int playerNumber);
}

public sealed class DeviceSessionStateChangedEventArgs : EventArgs
{
    public DeviceSessionStateChangedEventArgs(DeviceState oldState, DeviceState newState)
    {
        OldState = oldState;
        NewState = newState;
    }

    public DeviceState OldState { get; }
    public DeviceState NewState { get; }
}

public sealed class DeviceSessionConnectionLostEventArgs : EventArgs
{
    public DeviceSessionConnectionLostEventArgs(Exception error) => Error = error;

    public Exception Error { get; }
}

public sealed class DeviceSessionConnectionFailedEventArgs : EventArgs
{
    public DeviceSessionConnectionFailedEventArgs(VirtualControllerError error) => Error = error;
    public VirtualControllerError Error { get; }
}
