using Microsoft.UI.Xaml;
using Shared;
using Shared.Windows;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using WiinUSoft;
using WiinUSoft.Services;
using WiinUSoft.Models;

namespace WiinUSoft.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly IPreferencesService _preferencesService;
    private readonly IDeviceDiscoveryService _deviceDiscoveryService;
    private readonly IDeviceConnectionService _deviceConnectionService;
    private readonly Func<DeviceInfo, DeviceViewModel?> _deviceFactory;
    private readonly Dictionary<string, DeviceViewModel> _devicesByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly IMainWindowOperations _operations;
    private readonly IUiDispatcher _uiDispatcher;
    private bool _suppressPreferenceWrites;
    private bool _disposed;
    private bool _applicationActive = true;
    private string _appTitleText = "WiitarThing";
    private string _versionText = "Version";
    private Visibility _debugBuildVisibility = Visibility.Collapsed;
    private bool _autoStartEnabled;
    private bool _startMinimizedEnabled;
    private bool _exclusiveBluetoothAccessEnabled;
    private bool _autoRefreshEnabled;
    private bool _useMicrosoftBluetoothStack;
    private bool _isSyncEnabled = true;
    private bool _isAutoRefreshRingActive;
    private Visibility _autoRefreshRingVisibility = Visibility.Collapsed;
    private string _autoRefreshStatusText = "Auto refresh off";

    public MainWindowViewModel(
        IPreferencesService preferencesService,
        IDeviceDiscoveryService deviceDiscoveryService,
        IDeviceConnectionService deviceConnectionService,
        Func<DeviceInfo, DeviceViewModel?> deviceFactory,
        IMainWindowOperations operations,
        IUiDispatcher? uiDispatcher = null)
    {
        _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        _deviceDiscoveryService = deviceDiscoveryService ?? throw new ArgumentNullException(nameof(deviceDiscoveryService));
        _deviceConnectionService = deviceConnectionService ?? throw new ArgumentNullException(nameof(deviceConnectionService));
        _deviceFactory = deviceFactory ?? throw new ArgumentNullException(nameof(deviceFactory));
        _operations = operations ?? throw new ArgumentNullException(nameof(operations));
        _uiDispatcher = uiDispatcher ?? new InlineUiDispatcher();

        AvailableDevices = new ObservableCollection<DeviceViewModel>();
        ConnectedDevices = new ObservableCollection<DeviceViewModel>();

        RefreshCommand = new RelayCommand(RefreshDevices);
        SyncCommand = new AsyncRelayCommand(async () =>
        {
            IsSyncEnabled = false;
            try
            {
            await _operations.SynchronizeAsync().ConfigureAwait(true);
            }
            finally
            {
                IsSyncEnabled = true;
            }
        }, () => IsSyncEnabled, LogCommandError);
        DisconnectAllCommand = new RelayCommand(_operations.DisconnectAllControllers, () => ConnectedDevices.Count > 0);
        RemoveAllWiimotesCommand = new AsyncRelayCommand(_ => _operations.RemoveAllWiimotesAsync(), onError: LogCommandError);
        TestControllersCommand = new RelayCommand(_operations.OpenControllerTestPanel);
        SetDefaultCalibrationCommand = new AsyncRelayCommand(_ => _operations.ShowDefaultCalibrationAsync(), onError: LogCommandError);
        OpenUiGalleryCommand = new RelayCommand(_operations.OpenUiGallery);

        LoadFromPreferences();
    }

    public ObservableCollection<DeviceViewModel> AvailableDevices { get; }
    public ObservableCollection<DeviceViewModel> ConnectedDevices { get; }

    public ICommand RefreshCommand { get; }
    public ICommand SyncCommand { get; }
    public ICommand DisconnectAllCommand { get; }
    public ICommand RemoveAllWiimotesCommand { get; }
    public ICommand TestControllersCommand { get; }
    public ICommand SetDefaultCalibrationCommand { get; }
    public ICommand OpenUiGalleryCommand { get; }

    public void RefreshDevices()
    {
        if (_disposed)
            return;
        var discoveryResult = _deviceDiscoveryService.DiscoverDevices();
        if (discoveryResult.IsError)
        {
            System.Diagnostics.Debug.WriteLine(discoveryResult.Error.ToDisplayString());
            return;
        }

        var devices = discoveryResult.Value;
        var presentPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var deviceInfo in devices)
        {
            if (string.IsNullOrWhiteSpace(deviceInfo.DevicePath))
                continue;

            presentPaths.Add(deviceInfo.DevicePath);
            if (_devicesByPath.TryGetValue(deviceInfo.DevicePath, out DeviceViewModel? existing))
            {
                if (!existing.Session.Connected)
                {
                    existing.Session.Refresh();
                    AddAutoConnectCandidate(existing);
                }

                continue;
            }

            var created = _deviceFactory(deviceInfo);
            if (created is null)
                continue;

            created.StateChanged += HandleDeviceStateChanged;
            created.ConnectionLost += HandleDeviceConnectionLost;
            _devicesByPath[created.DevicePath] = created;
            MoveDevice(created, created.Session.ConnectionState);
            AddAutoConnectCandidate(created);
        }

        foreach (var stale in _devicesByPath.Values
                     .Where(vm => !vm.Session.Connected && !presentPaths.Contains(vm.DevicePath))
                     .ToList())
        {
            _autoConnectCandidates.Remove(stale);
            stale.StateChanged -= HandleDeviceStateChanged;
            stale.ConnectionLost -= HandleDeviceConnectionLost;
            RemoveDevice(stale);
            stale.Dispose();
            _devicesByPath.Remove(stale.DevicePath);
        }

        ConnectAutoConnectCandidates();
    }

    private readonly List<DeviceViewModel> _autoConnectCandidates = new();

    private void AddAutoConnectCandidate(DeviceViewModel device)
    {
        if (device.Session.AutoConnectSlot > 0 && !_autoConnectCandidates.Contains(device))
            _autoConnectCandidates.Add(device);
    }

    private void ConnectAutoConnectCandidates()
    {
        int? firstAvailablePlayer = _deviceConnectionService.GetFirstAvailablePlayer();
        if (firstAvailablePlayer is null)
            return;

        int targetPlayer = firstAvailablePlayer.Value;
        foreach (var device in _autoConnectCandidates.OrderBy(vm => vm.Session.AutoConnectSlot == 5 ? 0 : 1).ThenBy(vm => vm.Session.AutoConnectSlot).ToList())
        {
            if (targetPlayer > 4)
                break;

            if (_deviceConnectionService.TryConnectToXInput(device.Session, targetPlayer))
                targetPlayer++;
        }

        _autoConnectCandidates.Clear();
    }

    private void HandleDeviceStateChanged(object? sender, DeviceSessionStateChangedEventArgs e)
    {
        if (sender is DeviceViewModel device)
            _uiDispatcher.Enqueue(() =>
            {
                if (_devicesByPath.TryGetValue(device.DevicePath, out var current) && ReferenceEquals(current, device))
                    MoveDevice(device, device.Session.ConnectionState);
            });
    }

    private void HandleDeviceConnectionLost(object? sender, DeviceSessionConnectionLostEventArgs e)
    {
        if (sender is not DeviceViewModel device)
            return;

        _uiDispatcher.Enqueue(() =>
        {
            if (!_devicesByPath.TryGetValue(device.DevicePath, out var current) || !ReferenceEquals(current, device))
                return;
            _autoConnectCandidates.Remove(device);
            device.StateChanged -= HandleDeviceStateChanged;
            device.ConnectionLost -= HandleDeviceConnectionLost;
            RemoveDevice(device);
            _devicesByPath.Remove(device.DevicePath);
            device.Dispose();
        });
    }

    public string AppTitleText
    {
        get => _appTitleText;
        set => SetProperty(ref _appTitleText, value);
    }

    public string VersionText
    {
        get => _versionText;
        set => SetProperty(ref _versionText, value);
    }

    public Visibility DebugBuildVisibility
    {
        get => _debugBuildVisibility;
        set => SetProperty(ref _debugBuildVisibility, value);
    }

    public bool AutoStartEnabled
    {
        get => _autoStartEnabled;
        set
        {
            if (!SetProperty(ref _autoStartEnabled, value))
                return;

            if (_suppressPreferenceWrites)
                return;

            _preferencesService.AutoStartEnabled = value;
            SavePreferences();
        }
    }

    public bool StartMinimizedEnabled
    {
        get => _startMinimizedEnabled;
        set
        {
            if (!SetProperty(ref _startMinimizedEnabled, value))
                return;

            if (_suppressPreferenceWrites)
                return;

            _preferencesService.StartMinimizedEnabled = value;
            SavePreferences();
        }
    }

    public bool ExclusiveBluetoothAccessEnabled
    {
        get => _exclusiveBluetoothAccessEnabled;
        set
        {
            if (!SetProperty(ref _exclusiveBluetoothAccessEnabled, value))
                return;

            if (_suppressPreferenceWrites)
                return;

            _preferencesService.ExclusiveBluetoothAccessEnabled = value;
            SavePreferences();
        }
    }

    public bool AutoRefreshEnabled
    {
        get => _autoRefreshEnabled;
        set
        {
            if (!SetProperty(ref _autoRefreshEnabled, value))
                return;

            if (_suppressPreferenceWrites)
                return;

            _preferencesService.AutoRefreshEnabled = value;
            SavePreferences();
            ApplyAutoRefreshState();
        }
    }

    public bool UseMicrosoftBluetoothStack
    {
        get => _useMicrosoftBluetoothStack;
        set
        {
            if (!SetProperty(ref _useMicrosoftBluetoothStack, value))
                return;

            if (_suppressPreferenceWrites)
                return;

            _preferencesService.UseMicrosoftBluetoothStack = value;
            SavePreferences();
        }
    }

    public bool IsSyncEnabled
    {
        get => _isSyncEnabled;
        set
        {
            if (!SetProperty(ref _isSyncEnabled, value))
                return;

            if (SyncCommand is AsyncRelayCommand asyncRelayCommand)
                asyncRelayCommand.NotifyCanExecuteChanged();
        }
    }

    public bool IsAutoRefreshRingActive
    {
        get => _isAutoRefreshRingActive;
        private set => SetProperty(ref _isAutoRefreshRingActive, value);
    }

    public Visibility AutoRefreshRingVisibility
    {
        get => _autoRefreshRingVisibility;
        private set => SetProperty(ref _autoRefreshRingVisibility, value);
    }

    public string AutoRefreshStatusText
    {
        get => _autoRefreshStatusText;
        private set => SetProperty(ref _autoRefreshStatusText, value);
    }

    public void LoadFromPreferences()
    {
        _suppressPreferenceWrites = true;
        try
        {
            AutoStartEnabled = _preferencesService.AutoStartEnabled;
            StartMinimizedEnabled = _preferencesService.StartMinimizedEnabled;
            ExclusiveBluetoothAccessEnabled = _preferencesService.ExclusiveBluetoothAccessEnabled;
            AutoRefreshEnabled = _preferencesService.AutoRefreshEnabled;
            UseMicrosoftBluetoothStack = _preferencesService.UseMicrosoftBluetoothStack;
        }
        finally
        {
            _suppressPreferenceWrites = false;
        }

        ApplyAutoRefreshIndicator();
    }

    public void SetApplicationActive(bool isActive)
    {
        _applicationActive = isActive;
        ApplyAutoRefreshState();
        ApplyAutoRefreshIndicator();
    }

    public void MoveDevice(DeviceViewModel deviceViewModel, DeviceState state)
    {
        RemoveDeviceFromCollections(deviceViewModel);
        if (state == DeviceState.Discovered)
            AvailableDevices.Add(deviceViewModel);
        else if (state == DeviceState.Connected_XInput)
            ConnectedDevices.Add(deviceViewModel);

        RefreshDisconnectCommandState();
    }

    public void RemoveDevice(DeviceViewModel deviceViewModel)
    {
        RemoveDeviceFromCollections(deviceViewModel);
        RefreshDisconnectCommandState();
    }

    private void RemoveDeviceFromCollections(DeviceViewModel deviceViewModel)
    {
        _ = AvailableDevices.Remove(deviceViewModel);
        _ = ConnectedDevices.Remove(deviceViewModel);
    }

    private void ApplyAutoRefreshState()
    {
        _operations.SetAutoRefresh(AutoRefreshEnabled && _applicationActive);
        ApplyAutoRefreshIndicator();
    }

    private void ApplyAutoRefreshIndicator()
    {
        if (!AutoRefreshEnabled)
        {
            IsAutoRefreshRingActive = false;
            AutoRefreshRingVisibility = Visibility.Collapsed;
            AutoRefreshStatusText = "Auto refresh off";
            return;
        }

        IsAutoRefreshRingActive = _applicationActive;
        AutoRefreshRingVisibility = _applicationActive ? Visibility.Visible : Visibility.Collapsed;
        AutoRefreshStatusText = _applicationActive ? "Auto refresh on" : "Auto refresh paused";
    }

    private void SavePreferences()
    {
        Result<Unit, PreferencesError> result = _preferencesService.Save();
        if (result.IsError)
            WiitarDebug.Log(result.Error.ToDisplayString(), WiitarDebug.LogLevel.Error);
    }

    private static void LogCommandError(Exception exception)
        => WiitarDebug.Log($"Command failed: {exception}", WiitarDebug.LogLevel.Error);

    public void RefreshDisconnectCommandState()
    {
        if (DisconnectAllCommand is RelayCommand relayCommand)
            relayCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _operations.SetAutoRefresh(false);
        foreach (var device in _devicesByPath.Values.ToList())
        {
            device.StateChanged -= HandleDeviceStateChanged;
            device.ConnectionLost -= HandleDeviceConnectionLost;
            device.Dispose();
        }
        _devicesByPath.Clear();
        _autoConnectCandidates.Clear();
        AvailableDevices.Clear();
        ConnectedDevices.Clear();
        RefreshDisconnectCommandState();
    }
}
