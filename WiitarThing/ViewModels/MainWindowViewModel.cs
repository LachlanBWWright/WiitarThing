using Microsoft.UI.Xaml;
using Shared;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using WiinUSoft;
using WiinUSoft.Services;

namespace WiinUSoft.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IPreferencesService _preferencesService;
    private readonly Action _refreshAction;
    private readonly Func<Task> _syncAction;
    private readonly Action _disconnectAllAction;
    private readonly Func<Task> _removeAllWiimotesAction;
    private readonly Action _testControllersAction;
    private readonly Func<Task> _showDefaultCalibrationAction;
    private readonly Action<bool> _setAutoRefreshAction;
    private bool _suppressPreferenceWrites;
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
        Action refreshAction,
        Func<Task> syncAction,
        Action disconnectAllAction,
        Func<Task> removeAllWiimotesAction,
        Action testControllersAction,
        Func<Task> showDefaultCalibrationAction,
        Action<bool> setAutoRefreshAction)
    {
        _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        _refreshAction = refreshAction ?? throw new ArgumentNullException(nameof(refreshAction));
        _syncAction = syncAction ?? throw new ArgumentNullException(nameof(syncAction));
        _disconnectAllAction = disconnectAllAction ?? throw new ArgumentNullException(nameof(disconnectAllAction));
        _removeAllWiimotesAction = removeAllWiimotesAction ?? throw new ArgumentNullException(nameof(removeAllWiimotesAction));
        _testControllersAction = testControllersAction ?? throw new ArgumentNullException(nameof(testControllersAction));
        _showDefaultCalibrationAction = showDefaultCalibrationAction ?? throw new ArgumentNullException(nameof(showDefaultCalibrationAction));
        _setAutoRefreshAction = setAutoRefreshAction ?? throw new ArgumentNullException(nameof(setAutoRefreshAction));

        AvailableDevices = new ObservableCollection<DeviceViewModel>();
        ConnectedDevices = new ObservableCollection<DeviceViewModel>();

        RefreshCommand = new RelayCommand(_refreshAction);
        SyncCommand = new AsyncRelayCommand(async () =>
        {
            IsSyncEnabled = false;
            try
            {
                await _syncAction().ConfigureAwait(true);
            }
            finally
            {
                IsSyncEnabled = true;
            }
        }, () => IsSyncEnabled);
        DisconnectAllCommand = new RelayCommand(_disconnectAllAction, () => ConnectedDevices.Count > 0);
        RemoveAllWiimotesCommand = new AsyncRelayCommand(_ => _removeAllWiimotesAction());
        TestControllersCommand = new RelayCommand(_testControllersAction);
        SetDefaultCalibrationCommand = new AsyncRelayCommand(_ => _showDefaultCalibrationAction());

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
        _setAutoRefreshAction(AutoRefreshEnabled && _applicationActive);
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
            System.Diagnostics.Debug.WriteLine(result.Error.ToDisplayString());
    }

    public void RefreshDisconnectCommandState()
    {
        if (DisconnectAllCommand is RelayCommand relayCommand)
            relayCommand.NotifyCanExecuteChanged();
    }
}
