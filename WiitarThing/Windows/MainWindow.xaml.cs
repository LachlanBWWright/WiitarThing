using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NintrollerLib;
using Shared;
using Shared.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WiinUSoft.Services;
using WiinUSoft.ViewModels;

namespace WiinUSoft
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);

        public static MainWindow? Instance { get; private set; }

        private readonly IPreferencesService _preferencesService;
        private readonly IExternalProcessService _externalProcessService;
        private readonly IDeviceDiscoveryService _deviceDiscoveryService;
        private readonly IDeviceConnectionService _deviceConnectionService;
        private readonly Dictionary<string, DeviceViewModel> _devicesByPath;
        private readonly MainWindowViewModel _viewModel;
        private TrayIconService _trayService = null!;
        private List<DeviceInfo> hidList = null!;
        private Task? _refreshTask;
        private CancellationTokenSource? _refreshToken;
        private bool _refreshing;
        private bool _loadedFired;
        private bool _syncDialogOpen;

        public MainWindowViewModel ViewModel => _viewModel;

        public MainWindow()
        {
            _preferencesService = new PreferencesService();
            _externalProcessService = new ExternalProcessService();
            _deviceDiscoveryService = new DeviceDiscoveryService();
            _deviceConnectionService = new DeviceConnectionService();
            _devicesByPath = new Dictionary<string, DeviceViewModel>(StringComparer.OrdinalIgnoreCase);
            hidList = new List<DeviceInfo>();

            _viewModel = new MainWindowViewModel(
                _preferencesService,
                refreshAction: Refresh,
                syncAction: RunSyncDialogAsync,
                disconnectAllAction: DisconnectAllConnectedControllers,
                removeAllWiimotesAction: RemoveAllWiimotesAsync,
                testControllersAction: _externalProcessService.OpenControllerTestPanel,
                showDefaultCalibrationAction: ShowDefaultCalibrationAsync,
                setAutoRefreshAction: AutoRefresh);

            InitializeComponent();
            Instance = this;
            if (Content is FrameworkElement root)
                root.DataContext = _viewModel;

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

            var entryAssembly = System.Reflection.Assembly.GetEntryAssembly();
            Version? version = entryAssembly == null ? null : entryAssembly.GetName().Version;
            string displayTitle = "WiitarThing " + (version != null
                ? string.Format("V{0}.{1}.{2}", version.Major, version.Minor, version.Revision)
                : string.Empty);

#if LOW_BANDWIDTH
            displayTitle += " - LIGHT VERSION";
#endif

            Title = displayTitle;
            _viewModel.AppTitleText = displayTitle;
            _viewModel.VersionText = version != null
                ? string.Format("Version {0}.{1}.{2}", version.Major, version.Minor, version.Revision)
                : "Version";

#if DEBUG
            _viewModel.DebugBuildVisibility = Visibility.Visible;
#else
            _viewModel.DebugBuildVisibility = Visibility.Collapsed;
#endif

            _preferencesService.ApplyRuntimeSettings();

            _trayService = new TrayIconService();
            _trayService.ShowRequested += (_, _) => DispatcherQueue.TryEnqueue(ShowWindow);
            _trayService.RefreshRequested += (_, _) => DispatcherQueue.TryEnqueue(Refresh);
            _trayService.ExitRequested += (_, _) => DispatcherQueue.TryEnqueue(() =>
            {
                DisconnectAllConnectedControllers();
                Application.Current.Exit();
            });

            AppWindow.Closing += async (_, args) =>
            {
                if (_viewModel.ConnectedDevices.Count > 0)
                {
                    args.Cancel = true;
                    var dlg = new ContentDialog
                    {
                        Title = "Close WiitarThing?",
                        Content = "ALL connected controllers will STOP WORKING!",
                        PrimaryButtonText = "Close",
                        CloseButtonText = "Cancel",
                        XamlRoot = Content.XamlRoot
                    };

                    if (await dlg.ShowAsync() == ContentDialogResult.Primary)
                    {
                        DisconnectAllConnectedControllers();
                        _trayService.Dispose();
                        Close();
                    }
                }
                else
                {
                    _trayService.Dispose();
                }
            };

            Activated += Window_Loaded;
            Activated += Window_Activated;
        }

        public static bool ApplicationIsActivated()
        {
            var activatedHandle = GetForegroundWindow();
            if (activatedHandle == IntPtr.Zero)
                return false;

            int procId = Process.GetCurrentProcess().Id;
            _ = GetWindowThreadProcessId(activatedHandle, out int activeProcId);
            return activeProcId == procId;
        }

        public void ShowWindow()
        {
            _trayService.Hide();
            Activate();
        }

        public void TriggerRefresh() => Refresh();

        public void ShowBalloon(string title, string message, int icon = 0, SystemSound? sound = null)
        {
            _trayService.ShowBalloon(title, message, icon);
            if (sound != null)
                sound.Play();
        }

        private static void LogDiscoveryResult(Result<List<DeviceInfo>, DeviceDiscoveryError> result)
        {
            if (result.IsError)
                Debug.WriteLine(result.Error.ToDisplayString());
        }

        private void Refresh()
        {
            var pathResult = _deviceDiscoveryService.DiscoverDevices();
            LogDiscoveryResult(pathResult);
            hidList = pathResult.ValueOr(_ => new List<DeviceInfo>());

            RemoveStaleDiscoveredDevices();
            var connectSeq = new List<KeyValuePair<int, DeviceViewModel>>();

            foreach (var hid in hidList)
            {
                if (_devicesByPath.TryGetValue(hid.DevicePath, out DeviceViewModel? existingViewModel))
                {
                    DeviceControl existingDevice = existingViewModel.View;
                    if (!existingDevice.Connected)
                    {
                        existingDevice.RefreshState();
                        if (existingDevice.properties.autoConnect && existingDevice.ConnectionState == DeviceState.Discovered)
                            connectSeq.Add(new KeyValuePair<int, DeviceViewModel>(existingDevice.properties.autoNum, existingViewModel));
                    }

                    continue;
                }

                var stream = new WinBtStream(
                    hid.DevicePath,
                    UserPrefs.Instance.toshibaMode ? WinBtStream.BtStack.Toshiba : WinBtStream.BtStack.Microsoft,
                    UserPrefs.Instance.greedyMode ? FileShare.None : FileShare.ReadWrite);
                var nintroller = new Nintroller(stream, hid.Type);

                var openResult = stream.TryOpenConnection();
                if (openResult.IsError)
                {
                    Debug.WriteLine(openResult.Error.ToDisplayString());
                    continue;
                }

                if (!stream.CanRead)
                    continue;

                var control = new DeviceControl(nintroller, hid.DevicePath);
                control.OnConnectStateChange += DeviceControl_OnConnectStateChange;
                control.OnConnectionLost += DeviceControl_OnConnectionLost;
                control.RefreshState();

                var deviceViewModel = new DeviceViewModel(control);
                _devicesByPath[hid.DevicePath] = deviceViewModel;
                _viewModel.MoveDevice(deviceViewModel, control.ConnectionState);

                if (control.properties.autoConnect)
                    connectSeq.Add(new KeyValuePair<int, DeviceViewModel>(control.properties.autoNum, deviceViewModel));
            }

            int? firstAvailablePlayer = _deviceConnectionService.GetFirstAvailablePlayer();
            if (firstAvailablePlayer == null)
                return;

            int targetPlayer = firstAvailablePlayer.Value;
            foreach (var entry in connectSeq.Where(p => p.Key == 5))
            {
                if (targetPlayer > 4)
                    break;

                if (_deviceConnectionService.TryConnectToXInput(entry.Value.View, targetPlayer))
                    targetPlayer++;
            }

            foreach (var entry in connectSeq.Where(p => p.Key != 5).OrderBy(p => p.Key))
            {
                if (targetPlayer > 4)
                    break;

                if (_deviceConnectionService.TryConnectToXInput(entry.Value.View, targetPlayer))
                    targetPlayer++;
            }
        }

        private void RemoveStaleDiscoveredDevices()
        {
            var presentPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var hid in hidList)
            {
                if (!string.IsNullOrWhiteSpace(hid.DevicePath))
                    presentPaths.Add(hid.DevicePath);
            }

            var staleDevices = _devicesByPath.Values
                .Where(vm => !vm.View.Connected && !presentPaths.Contains(vm.DevicePath))
                .Select(vm => vm.View)
                .ToList();

            foreach (var staleDevice in staleDevices)
                RemoveDeviceControl(staleDevice);
        }

        private void RemoveDeviceControl(DeviceControl deviceControl)
        {
            deviceControl.OnConnectStateChange -= DeviceControl_OnConnectStateChange;
            deviceControl.OnConnectionLost -= DeviceControl_OnConnectionLost;

            if (_devicesByPath.TryGetValue(deviceControl.DevicePath, out DeviceViewModel? vm))
            {
                _viewModel.RemoveDevice(vm);
                _devicesByPath.Remove(deviceControl.DevicePath);
            }

            deviceControl.DisposeControl();
            _viewModel.RefreshDisconnectCommandState();
        }

        private void AutoRefresh(bool set)
        {
            if (set && !_refreshing)
            {
                _refreshing = true;
                _refreshToken = new CancellationTokenSource();
                _refreshTask = new Task(() =>
                {
                    while (!_refreshToken.IsCancellationRequested)
                    {
                        Thread.Sleep(1000);
                        if (_refreshToken.IsCancellationRequested)
                            break;

                        DispatcherQueue.TryEnqueue(Refresh);
                    }

                    _refreshing = false;
                }, _refreshToken.Token);
                _refreshTask.Start();
            }
            else if (!set && _refreshing)
            {
                if (_refreshToken != null)
                    _refreshToken.Cancel();
                _refreshing = false;
            }
        }

        private void Window_Loaded(object? sender, object args)
        {
            if (_loadedFired)
                return;

            _loadedFired = true;
            Activated -= Window_Loaded;

            _viewModel.LoadFromPreferences();
            if (_viewModel.StartMinimizedEnabled && AppWindow.Presenter is OverlappedPresenter presenter)
                presenter.Minimize();

            Refresh();

            switch (_preferencesService.VirtualOutputMode)
            {
                case VirtualOutputMode.ScpXbox360:
                    _ = VirtualControllerDriverPrompt.CheckAtStartupAsync();
                    break;
                case VirtualOutputMode.VJoyExperimental:
                    _ = VirtualControllerDriverPrompt.CheckVJoyAtStartupAsync();
                    break;
                case VirtualOutputMode.HidMaestroExperimental:
                    _ = VirtualControllerDriverPrompt.CheckHidMaestroAtStartupAsync();
                    break;
            }

            _viewModel.SetApplicationActive(ApplicationIsActivated());
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (!_loadedFired)
                return;

            _viewModel.SetApplicationActive(args.WindowActivationState != WindowActivationState.Deactivated);
        }

        private void DeviceControl_OnConnectStateChange(DeviceControl sender, DeviceState oldState, DeviceState newState)
        {
            if (oldState == newState)
                return;

            if (_devicesByPath.TryGetValue(sender.DevicePath, out DeviceViewModel? vm))
                _viewModel.MoveDevice(vm, newState);

            _viewModel.SetApplicationActive(ApplicationIsActivated());
            _viewModel.RefreshDisconnectCommandState();
        }

        private void DeviceControl_OnConnectionLost(DeviceControl sender)
        {
            RemoveDeviceControl(sender);
            _viewModel.SetApplicationActive(ApplicationIsActivated());
        }

        private void DisconnectAllConnectedControllers()
        {
            var connected = _viewModel.ConnectedDevices.Select(vm => vm.View).ToList();
            foreach (DeviceControl device in connected)
                device.Detatch();
        }

        private async Task RunSyncDialogAsync()
        {
            if (_syncDialogOpen)
                return;

            _syncDialogOpen = true;
            var sync = new Windows.SyncDialog { XamlRoot = Content.XamlRoot };
            sync.NewDeviceFound += Sync_NewDeviceFound;
            try
            {
                await sync.ShowAsync();
            }
            finally
            {
                sync.NewDeviceFound -= Sync_NewDeviceFound;
                _syncDialogOpen = false;
                Refresh();
            }
        }

        private async void Sync_NewDeviceFound(object? sender, EventArgs args)
        {
            for (int i = 0; i < 30; i++)
            {
                DispatcherQueue.TryEnqueue(Refresh);
                await Task.Delay(1000);
            }
        }

        private async Task ShowDefaultCalibrationAsync()
        {
            var dialog = new Windows.DefaultCalibrationWindow { XamlRoot = Content.XamlRoot };
            await dialog.ShowAsync();
        }

        private async Task RemoveAllWiimotesAsync()
        {
            var confirmDlg = new ContentDialog
            {
                Title = "Remove All Wiimotes?",
                Content = "Are you sure you want to remove all Wii remotes from this PC?\n\nNote: this cannot be cancelled once it begins and may take a couple of minutes.",
                PrimaryButtonText = "Yes",
                CloseButtonText = "No",
                XamlRoot = Content.XamlRoot
            };

            if (await confirmDlg.ShowAsync() != ContentDialogResult.Primary)
                return;

            var removeDialog = new Windows.RemoveAllWiimotesWindow { XamlRoot = Content.XamlRoot };
            await removeDialog.ShowAsync();

            var restartDlg = new ContentDialog
            {
                Title = "Wiimotes Removed",
                Content = "WiitarThing will now restart.\n\nDon't forget to reconnect your controllers afterward!",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await restartDlg.ShowAsync();

            _externalProcessService.RestartApplicationAndExit();
        }
    }
}
