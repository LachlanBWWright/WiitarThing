using System;
using System.Windows.Input;
using WiinUSoft.Models;
using WiinUSoft;

namespace WiinUSoft.ViewModels;

public sealed class DeviceViewModel : ViewModelBase
{
    private readonly RelayCommand _disconnectCommand;

    public DeviceViewModel(DeviceControl view)
    {
        View = view ?? throw new ArgumentNullException(nameof(view));
        View.OnConnectStateChange += HandleStateChanged;
        _disconnectCommand = new RelayCommand(
            execute: () => View.Detatch(),
            canExecute: () => View.Connected);
    }

    public DeviceControl View { get; }

    public string DevicePath => View.DevicePath;

    public string DisplayName => string.IsNullOrWhiteSpace(View.dName) ? "Unknown Device" : View.dName;

    public bool IsConnected => View.ConnectionState == DeviceState.Connected_XInput;

    public DeviceConnectionStatus Status => IsConnected
        ? DeviceConnectionStatus.Connected
        : View.ConnectionState == DeviceState.Discovered
            ? DeviceConnectionStatus.Discovered
            : DeviceConnectionStatus.Unknown;

    public ICommand DisconnectCommand => _disconnectCommand;

    private void HandleStateChanged(DeviceControl sender, DeviceState oldState, DeviceState newState)
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(Status));
        _disconnectCommand.NotifyCanExecuteChanged();
    }
}
