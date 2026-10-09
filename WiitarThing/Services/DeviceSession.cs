using System;
using System.Threading.Tasks;
using NintrollerLib;
using Shared;
using WiinUSoft.Holders;
using WiinUSoft.Models;
using WiinUSoft.VirtualOutput;
using WiinUSoft;

namespace WiinUSoft.Services;

/// <summary>
/// Hardware-backed device session. This is the application/model boundary;
/// it owns transport events, connection state, input processing and lifetime.
/// </summary>
internal sealed class DeviceSession : IConnectableDeviceSession
{
    private readonly ControllerSession _controllerSession;
    private readonly IDeviceConfigurationService _configuration;
    private readonly DeviceConnectionWorkflow _connectionWorkflow;
    private readonly DeviceStateCoordinator _stateCoordinator;
    private readonly VirtualOutputSession _virtualOutput;
    private readonly DeviceInputRuntime _inputRuntime = new();
    private readonly IUserNotificationService? _notifications;
    private readonly string _devicePath;
    private int _targetPlayer;
    private volatile DeviceState _state;
    private Property _properties = null!;
    private string _displayName = "Unknown Device";
    private bool _disposed;
    private readonly object _sessionGate = new();
    private System.Threading.CancellationTokenSource? _identifyCancellation;

    public DeviceSession(
        Nintroller controller,
        string devicePath,
        IDeviceConfigurationService configuration,
        IUserNotificationService? notifications = null,
        Func<VirtualOutputMode, IVirtualControllerBackend>? backendFactory = null,
        Func<Nintroller, Property, int, Result<Holder, VirtualControllerError>>? connectHolder = null)
    {
        _notifications = notifications;
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _connectionWorkflow = new DeviceConnectionWorkflow(_configuration);
        _virtualOutput = new VirtualOutputSession(_configuration, () => _properties, backendFactory);
        _stateCoordinator = new DeviceStateCoordinator(_connectionWorkflow, _virtualOutput, connectHolder);
        _controllerSession = new ControllerSession(controller);
        _devicePath = devicePath;
        _controllerSession.Controller.StateUpdate += HandleStateUpdate;
        _controllerSession.Controller.ExtensionChange += HandleExtensionChange;
        _controllerSession.Controller.LowBattery += HandleLowBattery;
        _controllerSession.Controller.Disconnected += HandleDisconnected;
    }

    public event EventHandler<DeviceSessionStateChangedEventArgs>? StateChanged;
    public event EventHandler<DeviceSessionConnectionLostEventArgs>? ConnectionLost;
    public event EventHandler<DeviceSessionConnectionFailedEventArgs>? ConnectionFailed;
    public event EventHandler? BatteryChanged;
    public event EventHandler? PreviewChanged;
    public event EventHandler<string>? InlineOutputError;

    public string DevicePath => _devicePath;
    public Nintroller Controller => _controllerSession.Controller;
    public string DisplayName
    {
        get => _displayName;
        set
        {
            var valueToUse = string.IsNullOrWhiteSpace(value) ? DeviceType.ToString() : value.Trim();
            if (string.Equals(_displayName, valueToUse, StringComparison.Ordinal))
                return;
            _displayName = valueToUse;
            _properties.name = valueToUse;
            _configuration.AddDevicePreference(_properties);
            SavePreferences();
        }
    }

    public bool Connected => _controllerSession.Controller.Connected;
    public DeviceState ConnectionState => _state;
    public int AutoConnectSlot
    {
        get => _properties?.autoNum ?? 0;
        set
        {
            if (_properties is null || _properties.autoNum == value)
                return;
            _properties.autoConnect = value > 0;
            _properties.autoNum = Math.Max(0, value);
            _configuration.AddDevicePreference(_properties);
            SavePreferences();
        }
    }

    public ControllerType DeviceType { get; private set; }
    internal Property Properties => _properties;
    internal Holder? CurrentHolder => _inputRuntime.Holder;
    internal int TargetPlayer => _targetPlayer;
    internal VirtualOutputSession VirtualOutput => _virtualOutput;
    public GuitarPreviewState LastPreviewState => ToPreviewState(_inputRuntime.LastSourceOutput);
    public bool IsBatteryLow => _controllerSession.Controller.BatteryLevel is BatteryStatus.Low or BatteryStatus.VeryLow;

    public void Refresh()
    {
        lock (_sessionGate)
        {
            if (_disposed)
                return;
            var snapshot = _configuration.LoadDevice(_devicePath, _controllerSession.Controller.Type);
            _properties = snapshot.Properties;
            _inputRuntime.Properties = _properties;
            _inputRuntime.SnapIrPointer = snapshot.SnapIrPointer;
            _displayName = snapshot.DisplayName;
            DeviceType = _controllerSession.Controller.Type;
            _connectionWorkflow.ApplyCalibration(_controllerSession.Controller, _properties, _properties.calString ?? "");
            SetState(DeviceState.Discovered);
        }
    }

    public void SetControllerType(ControllerType controllerType)
    {
        lock (_sessionGate)
        {
            if (_disposed)
                return;
            _controllerSession.Controller.ForceControllerType(controllerType);
            DeviceType = controllerType;
            Refresh();
        }
    }

    public Result<Unit, VirtualControllerError> AssignToXInput(int playerNumber)
    {
        lock (_sessionGate)
        {
            if (_disposed)
                return Result<Unit, VirtualControllerError>.Err(VirtualControllerError.ConnectionFailed("The device session is closed."));
            if (playerNumber < 1 || playerNumber > 4)
                return Result<Unit, VirtualControllerError>.Err(VirtualControllerError.SlotUnavailable(playerNumber));
            Disconnect();
            var connection = _stateCoordinator.Connect(_controllerSession.Controller, DeviceType, _properties, playerNumber);
            if (connection.Error is not null)
            {
                Disconnect();
                WiitarDebug.Log(connection.Error.ToDisplayString(), WiitarDebug.LogLevel.Error);
                ConnectionFailed?.Invoke(this, new DeviceSessionConnectionFailedEventArgs(connection.Error));
                return Result<Unit, VirtualControllerError>.Err(connection.Error);
            }

            _targetPlayer = playerNumber;
            _inputRuntime.Holder = connection.Holder;
            _controllerSession.BeginConnection();
            SetState(DeviceState.Connected_XInput);
            _connectionWorkflow.BeginXInput(_controllerSession.Controller);
            if (_disposed || !_controllerSession.Controller.Connected || _state != DeviceState.Connected_XInput)
            {
                Disconnect();
                return Result<Unit, VirtualControllerError>.Err(VirtualControllerError.ConnectionFailed("The controller transport failed while connecting."));
            }
            _inputRuntime.StartPolling(_controllerSession.Controller, playerNumber, Poll, 25);
            if (_disposed || !_controllerSession.Controller.Connected)
            {
                Disconnect();
                return Result<Unit, VirtualControllerError>.Err(VirtualControllerError.ConnectionFailed("The controller transport failed while assigning the player."));
            }
            return Result<Unit, VirtualControllerError>.Ok(Unit.Value);
        }
    }

    public async Task IdentifyAsync(bool wasConnected)
    {
        System.Threading.CancellationTokenSource cancellation;
        int targetPlayer;
        lock (_sessionGate)
        {
            if (_disposed || _identifyCancellation is not null)
                return;
            _identifyCancellation = cancellation = new();
            targetPlayer = _targetPlayer;
        }
        try
        {
            await _connectionWorkflow.IdentifyAsync(
                _controllerSession.Controller, wasConnected, targetPlayer,
                value => _inputRuntime.Identifying = value,
                cancellation.Token,
                action =>
                {
                    lock (_sessionGate)
                    {
                        if (!_disposed && ReferenceEquals(_identifyCancellation, cancellation))
                            action();
                    }
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Disconnect and disposal cancel the identify animation.
        }
        finally
        {
            lock (_sessionGate)
            {
                if (ReferenceEquals(_identifyCancellation, cancellation))
                {
                    _identifyCancellation = null;
                    _inputRuntime.Identifying = false;
                }
            }
            cancellation.Dispose();
        }
    }

    public void Disconnect()
    {
        lock (_sessionGate)
        {
            if (_disposed)
                return;
            _identifyCancellation?.Cancel();
            _identifyCancellation = null;
            _inputRuntime.Identifying = false;
            if (_controllerSession.Controller.Connected && _controllerSession.Controller.RumbleEnabled)
                _controllerSession.Controller.RumbleEnabled = false;
            _inputRuntime.StopPolling();
            _connectionWorkflow.Disconnect(_controllerSession.Controller, _inputRuntime.Holder);
            _inputRuntime.Holder = null;
            _virtualOutput.Disconnect();
            SetState(DeviceState.Discovered);
        }
    }

    public void Dispose()
    {
        lock (_sessionGate)
        {
            if (_disposed)
                return;
            Disconnect();
            _disposed = true;
            _inputRuntime.StopPolling();
            _virtualOutput.Dispose();
            _controllerSession.Controller.StateUpdate -= HandleStateUpdate;
            _controllerSession.Controller.ExtensionChange -= HandleExtensionChange;
            _controllerSession.Controller.LowBattery -= HandleLowBattery;
            _controllerSession.Controller.Disconnected -= HandleDisconnected;
            _controllerSession.Dispose();
        }
    }

    public bool TryReadVirtualOutput(out GuitarPreviewState state, out string? error)
    {
        lock (_sessionGate)
        {
            var result = _virtualOutput.Read();
            if (result.IsError)
            {
                state = GuitarPreviewState.Empty;
                error = result.Error.Message;
                return false;
            }
            state = ToPreviewState(result.Value);
            error = null;
            return true;
        }
    }

    public Result<Unit, VirtualControllerError> ReconnectVirtualOutput()
    {
        lock (_sessionGate)
        {
            if (_disposed || _state != DeviceState.Connected_XInput || DeviceType != ControllerType.Guitar)
                return Result<Unit, VirtualControllerError>.Err(VirtualControllerError.ConnectionFailed("The guitar is not connected."));
            var result = _virtualOutput.Connect(_targetPlayer, ControllerType.Guitar);
            if (result.IsError)
            {
                Disconnect();
                WiitarDebug.Log(result.Error.ToDisplayString(), WiitarDebug.LogLevel.Error);
                ConnectionFailed?.Invoke(this, new DeviceSessionConnectionFailedEventArgs(result.Error));
            }
            return result;
        }
    }

    private static GuitarPreviewState ToPreviewState(ControllerOutputState state)
        => new(state.Green, state.Red, state.Yellow, state.Blue, state.Orange,
            state.StrumUp, state.StrumDown, state.DPadLeft, state.DPadRight,
            state.Start, state.Select, state.Whammy, state.Tilt);

    private void SetState(DeviceState value)
    {
        if (_state == value)
            return;
        var previous = _state;
        _state = value;
        StateChanged?.Invoke(this, new DeviceSessionStateChangedEventArgs(previous, value));
    }

    private void SavePreferences()
    {
        var result = _configuration.Save();
        if (result.IsError)
            WiitarDebug.Log(result.Error.ToDisplayString(), WiitarDebug.LogLevel.Error);
    }

    private void HandleStateUpdate(object? sender, NintrollerStateEventArgs args)
    {
        lock (_sessionGate)
        {
            if (_disposed || _state != DeviceState.Connected_XInput)
                return;
            _inputRuntime.StepRumble(_controllerSession.Controller, _virtualOutput.Backend);
            _inputRuntime.ProcessState(
                args,
                _virtualOutput,
                () => PreviewChanged?.Invoke(this, EventArgs.Empty),
                (backend, error) =>
                {
                    if (!string.IsNullOrEmpty(error))
                        WiitarDebug.Log($"{backend}: {error}", WiitarDebug.LogLevel.Error);
                    InlineOutputError?.Invoke(this, string.IsNullOrEmpty(error) ? string.Empty : $"{backend}: {error}");
                },
                HandleOutputError);
        }
    }

    private void HandleOutputError(VirtualControllerError error)
    {
        WiitarDebug.Log(error.ToDisplayString(), WiitarDebug.LogLevel.Error);
        Disconnect();
        ConnectionFailed?.Invoke(this, new DeviceSessionConnectionFailedEventArgs(error));
    }

    private void HandleExtensionChange(object? sender, NintrollerExtensionEventArgs args)
    {
        lock (_sessionGate)
        {
            if (_disposed)
                return;
            DeviceType = args.controllerType;
            _inputRuntime.OnControllerTypeChanged(DeviceType);
            if (_state == DeviceState.Connected_XInput)
            {
                Disconnect();
                AssignToXInput(_targetPlayer);
            }
        }
    }

    private void HandleLowBattery(object? sender, LowBatteryEventArgs args)
    {
        lock (_sessionGate)
        {
            if (_disposed)
                return;
            BatteryChanged?.Invoke(this, EventArgs.Empty);
            if (IsBatteryLow)
                _notifications?.ShowBalloon("Battery Low", $"{DisplayName} is running low on battery life.", 2, System.Media.SystemSounds.Hand);
        }
    }

    private void HandleDisconnected(object? sender, DisconnectedEventArgs args)
    {
        lock (_sessionGate)
        {
            if (_disposed || !_controllerSession.TryAcceptConnectionLost())
                return;
            Disconnect();
            ConnectionLost?.Invoke(this, new DeviceSessionConnectionLostEventArgs(args.error));
            _notifications?.ShowBalloon("Connection Lost", "Failed to communicate with controller. It may no longer be connected.", 2);
        }
    }

    private void Poll(object? state)
    {
        lock (_sessionGate)
        {
            if (_disposed || _state != DeviceState.Connected_XInput || !_inputRuntime.IsCurrentPoll(state))
                return;
            _inputRuntime.Update(_controllerSession.Controller, _virtualOutput.Backend);
            BatteryChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
