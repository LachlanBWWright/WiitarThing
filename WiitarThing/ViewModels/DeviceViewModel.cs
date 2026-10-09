using System;
using System.Windows.Input;
using NintrollerLib;
using Shared;
using WiinUSoft.Models;
using WiinUSoft.Services;

namespace WiinUSoft.ViewModels;

public sealed class DeviceViewModel : ViewModelBase, IDisposable
{
    private readonly RelayCommand _disconnectCommand;
    private readonly RelayCommand _setControllerTypeCommand;
    private readonly IDeviceConfigurationService _configuration;
    private readonly IUiDispatcher _uiDispatcher;
    private bool _disposed;
    private string _outputError = string.Empty;

    public DeviceViewModel(IDeviceSession session, IDeviceConfigurationService? configuration = null, IUiDispatcher? uiDispatcher = null)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        _configuration = configuration ?? new DeviceConfigurationService();
        _uiDispatcher = uiDispatcher ?? new InlineUiDispatcher();
        Session.StateChanged += HandleStateChanged;
        Session.ConnectionLost += HandleConnectionLost;
        Session.ConnectionFailed += HandleConnectionFailed;
        Session.InlineOutputError += HandleInlineOutputError;
        _disconnectCommand = new RelayCommand(
            execute: Session.Disconnect,
            canExecute: () => IsConnected);
        _setControllerTypeCommand = new RelayCommand(
            execute: parameter =>
            {
                if (parameter is string value && Enum.TryParse<ControllerType>(value, out var type))
                    Session.SetControllerType(type);
            });
    }

    public IDeviceSession Session { get; }

    public event EventHandler<DeviceSessionConnectionLostEventArgs>? ConnectionLost;
    public event EventHandler<DeviceSessionStateChangedEventArgs>? StateChanged;

    public string DevicePath => Session.DevicePath;

    public string DisplayName
    {
        get => Session.DisplayName;
        set
        {
            if (string.Equals(value, Session.DisplayName, StringComparison.Ordinal))
                return;
            Session.DisplayName = value;
            OnPropertyChanged();
        }
    }

    public int AutoConnectSlot
    {
        get => Session.AutoConnectSlot;
        set
        {
            if (value == Session.AutoConnectSlot)
                return;
            Session.AutoConnectSlot = value;
            OnPropertyChanged();
        }
    }

    public int VirtualOutputModeIndex
    {
        get => (int)(_configuration.GetDevicePreference(DevicePath)?.virtualOutputMode ?? _configuration.VirtualOutputMode);
        set
        {
            if (!Enum.IsDefined(typeof(VirtualOutputMode), value) || value == VirtualOutputModeIndex)
                return;
            var properties = _configuration.LoadDevice(DevicePath, Session.DeviceType).Properties;
            properties.virtualOutputMode = (VirtualOutputMode)value;
            _configuration.AddDevicePreference(properties);
            SavePreferences();
            if (Session.ConnectionState == DeviceState.Connected_XInput && Session.DeviceType == ControllerType.Guitar)
            {
                if (Session.ReconnectVirtualOutput().IsOk)
                    OutputError = string.Empty;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(GuitarPreviewModeIndex));
            OnPropertyChanged(nameof(VJoySettingsVisibility));
        }
    }

    public int GuitarPreviewModeIndex
    {
        get => (int)(_configuration.GetDevicePreference(DevicePath)?.guitarPreviewMode ?? _configuration.GuitarPreviewMode);
        set
        {
            if (!Enum.IsDefined(typeof(GuitarPreviewMode), value) || value == GuitarPreviewModeIndex)
                return;
            var properties = _configuration.LoadDevice(DevicePath, Session.DeviceType).Properties;
            properties.guitarPreviewMode = (GuitarPreviewMode)value;
            _configuration.AddDevicePreference(properties);
            SavePreferences();
            OnPropertyChanged();
        }
    }

    public int VJoyDeviceId
    {
        get => Math.Max(1, _configuration.GetDevicePreference(DevicePath)?.vJoyDeviceId ?? _configuration.VJoyDeviceId);
        set
        {
            if (value < 1 || value > 255 || value == VJoyDeviceId)
                return;
            var properties = _configuration.LoadDevice(DevicePath, Session.DeviceType).Properties;
            properties.vJoyDeviceId = value;
            _configuration.AddDevicePreference(properties);
            SavePreferences();
            if (IsConnected && VirtualOutputModeIndex == (int)VirtualOutputMode.VJoyExperimental)
            {
                if (Session.ReconnectVirtualOutput().IsOk)
                    OutputError = string.Empty;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(VJoyDeviceIdValue));
        }
    }

    public double VJoyDeviceIdValue
    {
        get => VJoyDeviceId;
        set
        {
            if (double.IsFinite(value) && value == Math.Truncate(value) && value >= 1 && value <= 255)
                VJoyDeviceId = (int)value;
            OnPropertyChanged();
        }
    }

    public Microsoft.UI.Xaml.Visibility VJoySettingsVisibility => VirtualOutputModeIndex == (int)VirtualOutputMode.VJoyExperimental
        ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public bool IsConnected => Session.ConnectionState == DeviceState.Connected_XInput;

    public string OutputError
    {
        get => _outputError;
        private set
        {
            if (SetProperty(ref _outputError, value))
                OnPropertyChanged(nameof(OutputErrorVisibility));
        }
    }

    public Microsoft.UI.Xaml.Visibility OutputErrorVisibility => string.IsNullOrEmpty(OutputError)
        ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;

    public DeviceConnectionStatus Status => IsConnected
        ? DeviceConnectionStatus.Connected
        : Session.ConnectionState == DeviceState.Discovered
            ? DeviceConnectionStatus.Discovered
            : DeviceConnectionStatus.Unknown;

    public ICommand DisconnectCommand => _disconnectCommand;

    public ICommand SetControllerTypeCommand => _setControllerTypeCommand;

    public void ConnectToXInput(int playerNumber)
    {
        if (Session is IConnectableDeviceSession connectable)
            connectable.AssignToXInput(playerNumber);
    }

    public System.Threading.Tasks.Task IdentifyAsync()
        => Session.IdentifyAsync(Session.Connected);

    private void HandleStateChanged(object? sender, DeviceSessionStateChangedEventArgs e)
    {
        _uiDispatcher.Enqueue(() =>
        {
            if (_disposed)
                return;
            if (e.NewState == DeviceState.Connected_XInput)
                OutputError = string.Empty;
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(VirtualOutputModeIndex));
            OnPropertyChanged(nameof(GuitarPreviewModeIndex));
            OnPropertyChanged(nameof(VJoySettingsVisibility));
            OnPropertyChanged(nameof(VJoyDeviceIdValue));
            _disconnectCommand.NotifyCanExecuteChanged();
            StateChanged?.Invoke(this, e);
        });
    }

    private void HandleConnectionLost(object? sender, DeviceSessionConnectionLostEventArgs e)
        => _uiDispatcher.Enqueue(() =>
        {
            if (!_disposed)
                ConnectionLost?.Invoke(this, e);
        });

    private void HandleConnectionFailed(object? sender, DeviceSessionConnectionFailedEventArgs e)
        => _uiDispatcher.Enqueue(() =>
        {
            if (!_disposed)
                OutputError = e.Error.ToDisplayString();
        });

    private void HandleInlineOutputError(object? sender, string error)
        => _uiDispatcher.Enqueue(() =>
        {
            if (!_disposed)
                OutputError = error;
        });

    private void SavePreferences()
    {
        var result = _configuration.Save();
        if (result.IsError)
            WiitarDebug.Log(result.Error.ToDisplayString(), WiitarDebug.LogLevel.Error);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Session.StateChanged -= HandleStateChanged;
        Session.ConnectionLost -= HandleConnectionLost;
        Session.ConnectionFailed -= HandleConnectionFailed;
        Session.InlineOutputError -= HandleInlineOutputError;
        Session.Dispose();
    }
}
