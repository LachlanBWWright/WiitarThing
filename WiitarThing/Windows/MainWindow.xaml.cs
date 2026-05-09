using NintrollerLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Shared;
using Shared.Windows;
using System.Diagnostics;

namespace WiinUSoft
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);

        public static bool ApplicationIsActivated()
        {
            var activatedHandle = GetForegroundWindow();
            if (activatedHandle == IntPtr.Zero) return false;
            var procId = Process.GetCurrentProcess().Id;
            GetWindowThreadProcessId(activatedHandle, out int activeProcId);
            return activeProcId == procId;
        }

        public static MainWindow Instance { get; private set; }

        private TrayIconService _trayService;
        private List<DeviceInfo> hidList;
        private List<DeviceControl> deviceList;
        private List<DeviceControl> _availableDevices;
        private List<DeviceControl> _connectedDevices;
        private Task _refreshTask;
        private CancellationTokenSource _refreshToken;
        private bool _refreshing;
        private bool _loadedFired;
        private bool _syncDialogOpen;

        public MainWindow()
        {
            hidList = new List<DeviceInfo>();
            deviceList = new List<DeviceControl>();
            _availableDevices = new List<DeviceControl>();
            _connectedDevices = new List<DeviceControl>();

            InitializeComponent();
            Instance = this;
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            Version version = System.Reflection.Assembly.GetEntryAssembly().GetName().Version;
            string displayTitle = "WiitarThing " + string.Format("V{0}.{1}.{2}", version.Major, version.Minor, version.Revision);
            Title = displayTitle;
#if DEBUG
            Title += " Debug Build";
#else
            labelDebugBuild.Visibility = Visibility.Collapsed;
#endif
#if LOW_BANDWIDTH
            Title += " - LIGHT VERSION";
            displayTitle += " - LIGHT VERSION";
#endif
            AppTitleText.Text = displayTitle;

            _trayService = new TrayIconService();
            _trayService.ShowRequested += (s, e) => DispatcherQueue.TryEnqueue(ShowWindow);
            _trayService.RefreshRequested += (s, e) => DispatcherQueue.TryEnqueue(Refresh);
            _trayService.ExitRequested += (s, e) => DispatcherQueue.TryEnqueue(() =>
            {
                var dl = new List<DeviceControl>(_connectedDevices);
                foreach (var d in dl) d.Detatch();
                Application.Current.Exit();
            });

            AppWindow.Closing += async (sender2, args) =>
            {
                if (_connectedDevices.Count > 0)
                {
                    args.Cancel = true;
                    var dlg = new ContentDialog
                    {
                        Title = "Close WiitarThing?",
                        Content = "ALL connected controllers will STOP WORKING!",
                        PrimaryButtonText = "Close",
                        CloseButtonText = "Cancel",
                        XamlRoot = this.Content.XamlRoot
                    };
                    if (await dlg.ShowAsync() == ContentDialogResult.Primary)
                    {
                        var dl = new List<DeviceControl>(_connectedDevices);
                        foreach (var d in dl) d.Detatch();
                        _trayService?.Dispose();
                        this.Close();
                    }
                }
                else
                {
                    _trayService?.Dispose();
                }
            };

            this.Activated += Window_Loaded;
        }

        // ── Public interface ────────────────────────────────────────────────

        public void ShowWindow()
        {
            _trayService?.Hide();
            this.Activate();
        }

        public void TriggerRefresh() => Refresh();

        public void ShowBalloon(string title, string message, int icon = 0, SystemSound sound = null)
        {
            _trayService?.ShowBalloon(title, message, icon);
            sound?.Play();
        }

        // ── Refresh / device management ────────────────────────────────────

        private void Refresh()
        {
            hidList = WinBtStream.GetPaths();
            var connectSeq = new List<KeyValuePair<int, DeviceControl>>();

            foreach (var hid in hidList)
            {
                DeviceControl existingDevice = null;
                foreach (DeviceControl d in deviceList)
                {
                    if (d.DevicePath == hid.DevicePath) { existingDevice = d; break; }
                }

                if (existingDevice != null)
                {
                    if (!existingDevice.Connected)
                    {
                        existingDevice.RefreshState();
                        if (existingDevice.properties.autoConnect && existingDevice.ConnectionState == DeviceState.Discovered)
                            connectSeq.Add(new KeyValuePair<int, DeviceControl>(existingDevice.properties.autoNum, existingDevice));
                    }
                }
                else
                {
                    var stream = new WinBtStream(
                        hid.DevicePath,
                        UserPrefs.Instance.toshibaMode ? WinBtStream.BtStack.Toshiba : WinBtStream.BtStack.Microsoft,
                        UserPrefs.Instance.greedyMode ? FileShare.None : FileShare.ReadWrite);
                    var n = new Nintroller(stream, hid.Type);

                    if (stream.OpenConnection() && stream.CanRead)
                    {
                        var dc = new DeviceControl(n, hid.DevicePath);
                        deviceList.Add(dc);
                        dc.OnConnectStateChange += DeviceControl_OnConnectStateChange;
                        dc.OnConnectionLost += DeviceControl_OnConnectionLost;
                        dc.RefreshState();
                        if (dc.properties.autoConnect)
                            connectSeq.Add(new KeyValuePair<int, DeviceControl>(dc.properties.autoNum, dc));
                    }
                }
            }

            int target = -1;
            for (int i = 0; i < 4; i++)
            {
                if (Holders.XInputHolder.availabe.Length > i && Holders.XInputHolder.availabe[i]) { target = i; break; }
            }
            if (target < 0) return;

            for (int a = 0; a < connectSeq.Count; a++)
            {
                var thingy = connectSeq[a];
                if (thingy.Key == 5)
                {
                    if (Holders.XInputHolder.availabe[target] && target < 4)
                    {
                        if (thingy.Value.Device.Connected || (thingy.Value.Device.DataStream as WinBtStream).OpenConnection())
                        {
                            thingy.Value.targetXDevice = target + 1;
                            thingy.Value.ConnectionState = DeviceState.Connected_XInput;
                            thingy.Value.Device.BeginReading();
                            thingy.Value.Device.GetStatus();
                            thingy.Value.Device.SetPlayerLED(target + 1);
                            target++;
                        }
                    }
                    connectSeq.Remove(thingy);
                }
            }

            for (int i = 1; i < connectSeq.Count; i++)
            {
                if (connectSeq[i].Key < connectSeq[i - 1].Key)
                {
                    var tmp = connectSeq[i]; connectSeq[i] = connectSeq[i - 1]; connectSeq[i - 1] = tmp; i = 0;
                }
            }

            foreach (var d in connectSeq)
            {
                if (Holders.XInputHolder.availabe[target] && target < 4)
                {
                    if (d.Value.Device.Connected || (d.Value.Device.DataStream as WinBtStream).OpenConnection())
                    {
                        d.Value.targetXDevice = target + 1;
                        d.Value.ConnectionState = DeviceState.Connected_XInput;
                        d.Value.Device.BeginReading();
                        d.Value.Device.GetStatus();
                        d.Value.Device.SetPlayerLED(target + 1);
                        target++;
                    }
                }
            }
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
                        if (_refreshToken.IsCancellationRequested) break;
                        DispatcherQueue.TryEnqueue(Refresh);
                    }
                    _refreshing = false;
                }, _refreshToken.Token);
                _refreshTask.Start();
            }
            else if (!set && _refreshing)
            {
                _refreshToken.Cancel();
            }
        }

        // ── Event handlers ──────────────────────────────────────────────────

        private void Window_Loaded(object sender, object e)
        {
            if (_loadedFired) return;
            _loadedFired = true;
            this.Activated -= Window_Loaded;

            try
            {
                var v = System.Reflection.Assembly.GetEntryAssembly().GetName().Version;
                menu_version.Text = string.Format("Version {0}.{1}.{2}", v.Major, v.Minor, v.Revision);
            }
            catch { }

            if (UserPrefs.Instance.startMinimized)
            {
                menu_StartMinimized.IsChecked = true;
                if (this.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter p)
                    p.Minimize();
            }

            menu_AutoStart.IsChecked = UserPrefs.Instance.autoStartup;
            menu_NoSharing.IsChecked = UserPrefs.Instance.greedyMode;
            menu_AutoRefresh.IsChecked = UserPrefs.Instance.autoRefresh;
            menu_MsBluetooth.IsChecked = !UserPrefs.Instance.toshibaMode;

            if (UserPrefs.Instance.greedyMode)
            {
                WinBtStream.OverrideSharingMode = true;
                WinBtStream.OverridenFileShare = FileShare.None;
            }

            Refresh();
            AutoRefresh(menu_AutoRefresh.IsChecked && ApplicationIsActivated());
        }

        private void DeviceControl_OnConnectStateChange(DeviceControl sender, DeviceState oldState, DeviceState newState)
        {
            if (oldState == newState) return;
            switch (oldState)
            {
                case DeviceState.Discovered: groupAvailable.Children.Remove(sender); _availableDevices.Remove(sender); break;
                case DeviceState.Connected_XInput: groupXinput.Children.Remove(sender); _connectedDevices.Remove(sender); break;
            }
            switch (newState)
            {
                case DeviceState.Discovered: groupAvailable.Children.Add(sender); _availableDevices.Add(sender); break;
                case DeviceState.Connected_XInput: groupXinput.Children.Add(sender); _connectedDevices.Add(sender); break;
            }
            if (menu_AutoRefresh.IsChecked) AutoRefresh(ApplicationIsActivated());
        }

        private void DeviceControl_OnConnectionLost(DeviceControl sender)
        {
            groupAvailable.Children.Remove(sender);
            groupXinput.Children.Remove(sender);
            _availableDevices.Remove(sender);
            _connectedDevices.Remove(sender);
            deviceList.Remove(sender);
            AutoRefresh(menu_AutoRefresh.IsChecked);
        }

        private void btnDetatchAllXInput_Click(object sender, RoutedEventArgs e)
        {
            var dl = new List<DeviceControl>(_connectedDevices);
            foreach (DeviceControl d in dl) d.Detatch();
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e) => Refresh();

        private async void btnSync_Click(object sender, RoutedEventArgs e)
        {
            if (_syncDialogOpen) return;

            _syncDialogOpen = true;
            menuSync.IsEnabled = false;
            var sync = new Windows.SyncDialog { XamlRoot = Content.XamlRoot };
            sync.NewDeviceFound += Sync_NewDeviceFound;
            try
            {
                await sync.ShowAsync();
            }
            finally
            {
                sync.NewDeviceFound -= Sync_NewDeviceFound;
                menuSync.IsEnabled = true;
                _syncDialogOpen = false;
            }
        }

        private void Sync_NewDeviceFound(object sender, EventArgs e)
        {
            DispatcherQueue.TryEnqueue(Refresh);
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e) { /* Flyout opens automatically */ }

        private void menu_AutoStart_Click(object sender, RoutedEventArgs e)
        {
            UserPrefs.AutoStart = menu_AutoStart.IsChecked;
            UserPrefs.SavePrefs();
        }

        private void menu_StartMinimized_Click(object sender, RoutedEventArgs e)
        {
            UserPrefs.Instance.startMinimized = menu_StartMinimized.IsChecked;
            UserPrefs.SavePrefs();
        }

        private void menu_NoSharing_Click(object sender, RoutedEventArgs e)
        {
            UserPrefs.Instance.greedyMode = menu_NoSharing.IsChecked;
            UserPrefs.SavePrefs();
            WinBtStream.OverrideSharingMode = UserPrefs.Instance.greedyMode;
            if (UserPrefs.Instance.greedyMode) WinBtStream.OverridenFileShare = FileShare.None;
        }

        private void menu_AutoRefresh_Click(object sender, RoutedEventArgs e)
        {
            UserPrefs.Instance.autoRefresh = menu_AutoRefresh.IsChecked;
            UserPrefs.SavePrefs();
            AutoRefresh(menu_AutoRefresh.IsChecked && ApplicationIsActivated());
        }

        private async void menu_SetDefaultCalibration_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Windows.DefaultCalibrationWindow { XamlRoot = Content.XamlRoot };
            await dialog.ShowAsync();
        }

        private void menu_MsBluetooth_Click(object sender, RoutedEventArgs e)
        {
            WinBtStream.ForceToshibaMode = !menu_MsBluetooth.IsChecked;
            UserPrefs.Instance.toshibaMode = !menu_MsBluetooth.IsChecked;
            UserPrefs.SavePrefs();
        }

        private async void btnRemoveAllWiimotes_Click(object sender, RoutedEventArgs e)
        {
            var confirmDlg = new ContentDialog
            {
                Title = "Remove All Wiimotes?",
                Content = "Are you sure you want to remove all Wii remotes from this PC?\n\nNote: this cannot be cancelled once it begins and may take a couple of minutes.",
                PrimaryButtonText = "Yes",
                CloseButtonText = "No",
                XamlRoot = this.Content.XamlRoot
            };
            if (await confirmDlg.ShowAsync() != ContentDialogResult.Primary) return;

            var dlg = new Windows.RemoveAllWiimotesWindow();
            await dlg.ShowAsDialogAsync();

            var restartDlg = new ContentDialog
            {
                Title = "Wiimotes Removed",
                Content = "WiitarThing will now restart.\n\nDon't forget to reconnect your controllers afterward!",
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };
            await restartDlg.ShowAsync();

            string exePath = Path.Combine(AppContext.BaseDirectory, "WiitarThing.exe");
            Process.Start(exePath);
            Application.Current.Exit();
        }

        private void buttonTestInputs_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("joy.cpl") { UseShellExecute = true });
        }

        // ── Shortcut creation ───────────────────────────────────────────────

        public void CreateShortcut(string path)
        {
            IShellLink link = (IShellLink)new ShellLink();
            link.SetDescription("WiinUSoft");
            link.SetPath(AppContext.BaseDirectory.TrimEnd('\\', '/'));
            IPersistFile file = (IPersistFile)link;
            file.Save(Path.Combine(path, "WiinUSoft.lnk"), false);
        }

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        internal class ShellLink { }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        internal interface IShellLink
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        static Task Delay(int ms)
        {
            var tcs = new TaskCompletionSource<object>();
            new System.Threading.Timer(_ => tcs.SetResult(null)).Change(ms, -1);
            return tcs.Task;
        }
    }
}
