using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NintrollerLib;
using Shared;
using WiinUSoft.Models;
using WiinUSoft.Services;
using WiinUSoft.ViewModels;

namespace WiinUSoft.Windows;

/// <summary>Exercises production bindings without Bluetooth, native drivers, or saved preferences.</summary>
internal sealed class GalleryDeviceFixture : IDisposable
{
    public GalleryDeviceFixture(string path, string name, ControllerType type, DeviceState state,
        VirtualOutputMode mode = VirtualOutputMode.ScpXbox360)
    {
        Configuration = new GalleryConfiguration(mode);
        Session = new GallerySession(path, name, type, state);
        ViewModel = new DeviceViewModel(Session, Configuration);
    }

    public GallerySession Session { get; }
    public GalleryConfiguration Configuration { get; }
    public DeviceViewModel ViewModel { get; }
    public DeviceControl Control { get; private set; } = null!;
    public void CreateControl() => Control = new DeviceControl { DataContext = ViewModel, Session = Session };
    public void Dispose() => ViewModel.Dispose();

    internal sealed class GalleryConfiguration(VirtualOutputMode mode) : IDeviceConfigurationService
    {
        private readonly Dictionary<string, Property> _devices = new();
        public VirtualOutputMode VirtualOutputMode { get; set; } = mode;
        public GuitarPreviewMode GuitarPreviewMode { get; set; }
        public int VJoyDeviceId => 2;
        public Profile? DefaultProfile => null;
        public int Saves { get; private set; }
        public DevicePreferencesSnapshot LoadDevice(string path, ControllerType type)
        {
            if (!_devices.TryGetValue(path, out var property))
                _devices[path] = property = new Property(path);
            return new(property, type.ToString(), false, null);
        }
        public Property? GetDevicePreference(string path) => _devices.GetValueOrDefault(path);
        public void AddDevicePreference(Property property) => _devices[property.hid] = property;
        public void UpdateDeviceIcon(string path, string iconKey) => LoadDevice(path, ControllerType.Guitar).Properties.lastIcon = iconKey;
        public Result<Unit, PreferencesError> Save()
        {
            Saves++;
            return Result<Unit, PreferencesError>.Ok(Unit.Value);
        }
    }

    internal sealed class GallerySession(string path, string name, ControllerType type, DeviceState state) : IConnectableDeviceSession
    {
        public event EventHandler<DeviceSessionStateChangedEventArgs>? StateChanged;
        public event EventHandler<DeviceSessionConnectionLostEventArgs>? ConnectionLost;
        public event EventHandler<DeviceSessionConnectionFailedEventArgs>? ConnectionFailed;
        public event EventHandler? BatteryChanged { add { } remove { } }
        public event EventHandler? PreviewChanged { add { } remove { } }
        public event EventHandler<string>? InlineOutputError { add { } remove { } }
        public string DevicePath => path;
        public string DisplayName { get; set; } = name;
        public bool Connected => ConnectionState == DeviceState.Connected_XInput;
        public DeviceState ConnectionState { get; private set; } = state;
        public int AutoConnectSlot { get; set; }
        public ControllerType DeviceType { get; private set; } = type;
        public bool IsBatteryLow => false;
        public GuitarPreviewState LastPreviewState => new(true, false, true, false, false,
            false, true, false, false, true, false, 0.6f, 0.3f);
        public Nintroller Controller { get; } = new(new MemoryStream());
        public bool FailReconnect { get; set; }
        public int Reconnects { get; private set; }
        public void Disconnect() => SetState(DeviceState.Discovered);
        public void Refresh() => StateChanged?.Invoke(this, new(ConnectionState, ConnectionState));
        public void SetControllerType(ControllerType controllerType) { DeviceType = controllerType; Refresh(); }
        public Task IdentifyAsync(bool wasConnected) => Task.CompletedTask;
        public bool TryReadVirtualOutput(out GuitarPreviewState preview, out string? error)
        {
            preview = LastPreviewState;
            error = null;
            return Connected;
        }
        public Result<Unit, VirtualControllerError> AssignToXInput(int playerNumber)
        {
            SetState(DeviceState.Connected_XInput);
            return Result<Unit, VirtualControllerError>.Ok(Unit.Value);
        }
        public Result<Unit, VirtualControllerError> ReconnectVirtualOutput()
        {
            Reconnects++;
            if (!FailReconnect) return Result<Unit, VirtualControllerError>.Ok(Unit.Value);
            SetState(DeviceState.Discovered);
            var error = VirtualControllerError.ConnectionFailed("Gallery fixture: selected virtual output could not be opened.");
            ConnectionFailed?.Invoke(this, new(error));
            return Result<Unit, VirtualControllerError>.Err(error);
        }
        public void LoseConnection()
        {
            SetState(DeviceState.None);
            ConnectionLost?.Invoke(this, new(new IOException("Gallery fixture: Bluetooth connection lost.")));
        }
        private void SetState(DeviceState next)
        {
            var previous = ConnectionState;
            ConnectionState = next;
            StateChanged?.Invoke(this, new(previous, next));
        }
        public void Dispose() => Controller.Dispose();
    }
}
