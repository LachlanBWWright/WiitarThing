using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Shared;
using Shared.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WiinUSoft.Services;
using WiinUSoft.ViewModels;

namespace WiinUSoft
{
    public partial class MainWindow : Window, IMainWindowOperations
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
        private readonly IUserNotificationService _notificationService;
        private readonly DeviceViewModelFactory _deviceViewModelFactory;
        private readonly IDeviceConfigurationService _configurationService;
        private readonly MainWindowViewModel _viewModel;
        private TrayIconService _trayService = null!;
        private Task? _refreshTask;
        private CancellationTokenSource? _refreshToken;
        private readonly SemaphoreSlim _refreshLifecycleGate = new(1, 1);
        private bool _loadedFired;
        private bool _syncDialogOpen;
        private Windows.DesignGalleryWindow? _designGalleryWindow;

        public MainWindowViewModel ViewModel => _viewModel;

        public MainWindow()
        {
            _preferencesService = new PreferencesService();
            _externalProcessService = new ExternalProcessService();
            _deviceDiscoveryService = new DeviceDiscoveryService();
            _deviceConnectionService = new DeviceConnectionService();
            _configurationService = new DeviceConfigurationService();

            InitializeComponent();
            _deviceViewModelFactory = new DeviceViewModelFactory(_configurationService, new DispatcherQueueUiDispatcher(DispatcherQueue));

            _viewModel = new MainWindowViewModel(
                _preferencesService,
                deviceDiscoveryService: _deviceDiscoveryService,
                deviceConnectionService: _deviceConnectionService,
                deviceFactory: hid => _deviceViewModelFactory.Create(hid, _notificationService),
                operations: this,
                uiDispatcher: new DispatcherQueueUiDispatcher(DispatcherQueue));
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
            UiGalleryMenuItem.Visibility = Visibility.Collapsed;
#endif

            _preferencesService.ApplyRuntimeSettings();

            _trayService = new TrayIconService();
            _notificationService = new TrayNotificationService(_trayService);
            _trayService.ShowRequested += (_, _) => DispatcherQueue.TryEnqueue(ShowWindow);
            _trayService.RefreshRequested += (_, _) => DispatcherQueue.TryEnqueue(_viewModel.RefreshDevices);
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
            Closed += (_, _) => _viewModel.Dispose();
        }

        public static bool ApplicationIsActivated()
        {
            var activatedHandle = GetForegroundWindow();
            if (activatedHandle == IntPtr.Zero)
                return false;

            int procId = Environment.ProcessId;
            _ = GetWindowThreadProcessId(activatedHandle, out int activeProcId);
            return activeProcId == procId;
        }

        public void ShowWindow()
        {
            _trayService.Hide();
            Activate();
        }

        public void TriggerRefresh() => _viewModel.RefreshDevices();

        Task IMainWindowOperations.SynchronizeAsync() => RunSyncDialogAsync();
        void IMainWindowOperations.DisconnectAllControllers() => DisconnectAllConnectedControllers();
        Task IMainWindowOperations.RemoveAllWiimotesAsync() => RemoveAllWiimotesAsync();
        void IMainWindowOperations.OpenControllerTestPanel() => _externalProcessService.OpenControllerTestPanel();
        Task IMainWindowOperations.ShowDefaultCalibrationAsync() => ShowDefaultCalibrationAsync();
        void IMainWindowOperations.OpenUiGallery() => OpenUiGallery();
        void IMainWindowOperations.SetAutoRefresh(bool enabled) => AutoRefresh(enabled);

        public void ShowBalloon(string title, string message, int icon = 0, SystemSound? sound = null)
        {
            _trayService.ShowBalloon(title, message, icon);
            if (sound != null)
                sound.Play();
        }

        private async void AutoRefresh(bool set)
        {
            await _refreshLifecycleGate.WaitAsync();
            try
            {
                if (set)
                {
                    if (_refreshTask is { IsCompleted: false })
                        return;

                    _refreshToken = new CancellationTokenSource();
                    _refreshTask = RunAutoRefreshLoopAsync(_refreshToken);
                    return;
                }

                CancellationTokenSource? tokenSource = _refreshToken;
                Task? refreshTask = _refreshTask;
                _refreshToken = null;
                _refreshTask = null;

                if (tokenSource == null)
                    return;

                tokenSource.Cancel();
                if (refreshTask != null)
                {
                    try
                    {
                        await refreshTask;
                    }
                    catch (OperationCanceledException ex)
                    {
                        Debug.WriteLine($"Auto-refresh loop cancelled: {ex.Message}");
                    }
                }

                tokenSource.Dispose();
            }
            finally
            {
                _refreshLifecycleGate.Release();
            }
        }

        private async Task RunAutoRefreshLoopAsync(CancellationTokenSource tokenSource)
        {
            CancellationToken token = tokenSource.Token;
            try
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), token);
                    if (token.IsCancellationRequested)
                        return;

                    DispatcherQueue.TryEnqueue(_viewModel.RefreshDevices);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
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

            _viewModel.RefreshDevices();

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

        private void DisconnectAllConnectedControllers()
        {
            foreach (var device in _viewModel.ConnectedDevices.ToList())
                device.Session.Disconnect();
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
                _viewModel.RefreshDevices();
            }
        }

        private void OpenUiGallery()
        {
            if (_designGalleryWindow != null)
            {
                _designGalleryWindow.Activate();
                return;
            }

            _designGalleryWindow = new Windows.DesignGalleryWindow();
            _designGalleryWindow.Closed += (_, _) => _designGalleryWindow = null;
            _ = _designGalleryWindow.ShowAsDialogAsync();
        }

        private async void Sync_NewDeviceFound(object? sender, EventArgs args)
        {
            for (int i = 0; i < 30; i++)
            {
                DispatcherQueue.TryEnqueue(_viewModel.RefreshDevices);
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
