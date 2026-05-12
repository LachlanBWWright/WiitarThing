using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI;
using NintrollerLib;
using Shared;
using Shared.Windows;
using Windows.System;
using WiinUSoft.VirtualOutput;

namespace WiinUSoft
{
    public enum DeviceState { None = 0, Discovered, Connected_XInput, Connected_VJoy }
    public delegate void ConnectStateChange(DeviceControl sender, DeviceState oldState, DeviceState newState);
    public delegate void ConnectionLost(DeviceControl sender);

    public partial class DeviceControl : UserControl
    {
        #region Members
        private string devicePath = string.Empty;
        private Nintroller device = null!;
        private DeviceState state;
        private IR previousIR;
        private bool snapIRpointer;
        private float rumbleAmount;
        private int rumbleStepCount;
        private int rumbleStepPeriod = 10;
        private float rumbleSlowMult = 0.5f;

        internal Holders.Holder? holder;
        internal Property properties = null!;
        internal int targetXDevice;
        internal bool lowBatteryFired;
        internal bool identifying;
        internal string dName = "";
        internal System.Threading.Timer? updateTimer;

        internal const int UPDATE_SPEED = 25;
        private static readonly SolidColorBrush PreviewInactiveBrush = new(Colors.Transparent);
        private static readonly SolidColorBrush PreviewTextInactiveBrush = new(Colors.Gray);
        private static readonly SolidColorBrush PreviewTextActiveBrush = new(Colors.White);
        private static readonly SolidColorBrush PreviewActiveBrush = new(Colors.White);
        private static readonly SolidColorBrush FretGBrush = new(global::Windows.UI.Color.FromArgb(255, 45, 164, 78));
        private static readonly SolidColorBrush FretRBrush = new(global::Windows.UI.Color.FromArgb(255, 209, 52, 56));
        private static readonly SolidColorBrush FretYBrush = new(global::Windows.UI.Color.FromArgb(255, 255, 200, 61));
        private static readonly SolidColorBrush FretBBrush = new(global::Windows.UI.Color.FromArgb(255, 0, 120, 212));
        private static readonly SolidColorBrush FretOBrush = new(global::Windows.UI.Color.FromArgb(255, 255, 140, 0));
        private IVirtualControllerBackend? virtualBackend;
        private IVirtualControllerReadback? virtualReadback;
        private VirtualControllerIdentity? virtualIdentity;
        private ControllerOutputState lastSourceOutput = ControllerOutputState.Empty;
        private ControllerOutputState lastReadbackOutput = ControllerOutputState.Empty;
        private bool backendWriteFailed;

        public event ConnectStateChange? OnConnectStateChange;
        public event ConnectionLost? OnConnectionLost;
        #endregion

        #region Properties
        internal Nintroller Device
        {
            get => device;
            set
            {
                if (device != null)
                {
                    device.ExtensionChange -= device_ExtensionChange;
                    device.StateUpdate -= device_StateChange;
                    device.LowBattery -= device_LowBattery;
#if DEBUG
                    device.StateUpdate -= Debug_Device_StateUpdate;
#endif
                }
                device = value;
                if (device != null)
                {
                    device.ExtensionChange += device_ExtensionChange;
                    device.StateUpdate += device_StateChange;
                    device.LowBattery += device_LowBattery;
#if DEBUG
                    device.StateUpdate += Debug_Device_StateUpdate;
#endif
                }
            }
        }

        internal ControllerType DeviceType { get; private set; }
        internal string DevicePath { get => devicePath; private set => devicePath = value; }
        internal bool Connected => device?.Connected == true;

        internal DeviceState ConnectionState
        {
            get => state;
            set
            {
                if (value != state)
                {
                    var prev = state;
                    SetState(value);
                    OnConnectStateChange?.Invoke(this, prev, value);
                }
            }
        }
        #endregion

        public DeviceControl() { InitializeComponent(); }

        public DeviceControl(Nintroller nintroller, string path) : this()
        {
            Device = nintroller;
            devicePath = path;
            Device.Disconnected += device_Disconnected;
        }

        internal void DisposeControl()
        {
            updateTimer?.Dispose();
            updateTimer = null;
            DisconnectVirtualBackend();

            if (device != null)
            {
                device.Disconnected -= device_Disconnected;
                device.Dispose();
            }
        }

#if DEBUG
        private Windows.DebugDataWindow? DebugDataWindowInstance;
        private bool _debugWindowVisible;

        private void Debug_Device_StateUpdate(object? sender, NintrollerStateEventArgs e)
        {
            if (e.state.DebugViewActive)
            {
                e.state.DebugViewActive = false;
                DebugViewActivate();
            }
        }

        private void DebugViewActivate()
        {
            if (!_debugWindowVisible)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    DebugDataWindowInstance = new Windows.DebugDataWindow();
                    DebugDataWindowInstance.nintroller = Device;
                    DebugDataWindowInstance.RegisterNintrollerUpdate();
                    DebugDataWindowInstance.Closed += (s, e2) => _debugWindowVisible = false;
                    DebugDataWindowInstance.XamlRoot = this.XamlRoot;
                    _debugWindowVisible = true;
                    _ = DebugDataWindowInstance.ShowAsync();
                });
            }
        }
#endif

        public void RefreshState()
        {
            if (state != DeviceState.Connected_XInput) ConnectionState = DeviceState.Discovered;
            Property? savedProperties = UserPrefs.Instance.GetDevicePref(devicePath);
            if (savedProperties != null)
            {
                properties = savedProperties;
                SetName(string.IsNullOrWhiteSpace(properties.name) ? device.Type.ToString() : properties.name);
                ApplyCalibration(properties.calPref, properties.calString ?? "");
                snapIRpointer = properties.pointerMode != Property.PointerOffScreenMode.Center;
                if (!string.IsNullOrEmpty(properties.lastIcon))
                    icon.Source = Application.Current.Resources[properties.lastIcon] as Microsoft.UI.Xaml.Media.ImageSource;
            }
            else
            {
                properties = new Property(devicePath);
                UpdateIcon(device.Type);
                SetName(device.Type.ToString());
            }

            autoConnectNumber.SelectionChanged -= AutoConnect_SelectionChanged;
            autoConnectNumber.SelectedIndex = properties.autoConnect
                ? Math.Clamp(properties.autoNum, 1, autoConnectNumber.Items.Count - 1)
                : 0;
            autoConnectNumber.SelectionChanged += AutoConnect_SelectionChanged;
            outputModeSelector.SelectionChanged -= outputModeSelector_SelectionChanged;
            outputModeSelector.SelectedIndex = (int)UserPrefs.Instance.virtualOutputMode;
            outputModeSelector.SelectionChanged += outputModeSelector_SelectionChanged;
            previewModeSelector.SelectionChanged -= previewModeSelector_SelectionChanged;
            previewModeSelector.SelectedIndex = (int)UserPrefs.Instance.guitarPreviewMode;
            previewModeSelector.SelectionChanged += previewModeSelector_SelectionChanged;
        }

        public void SetName(string newName)
        {
            dName = newName;
            labelName.Text = newName;
            nameInput.Text = newName;
        }

        public void Detatch()
        {
            device?.StopReading();
            holder?.Close();
            DisconnectVirtualBackend();
            lowBatteryFired = false;
            ConnectionState = DeviceState.Discovered;
            DispatcherQueue.TryEnqueue(() =>
                statusGradient.Background = new SolidColorBrush(global::Microsoft.UI.Colors.Transparent));
        }

        public void SetState(DeviceState newState)
        {
            state = newState;
            updateTimer?.Dispose();
            updateTimer = null;

            switch (newState)
            {
                case DeviceState.None:
                    btnIdentify.IsEnabled = false;
                    btnEditName.IsEnabled = false;
                    autoConnectNumber.IsEnabled = false;
                    btnXinput.IsEnabled = false;
                    btnDetatch.IsEnabled = false;
                    btnDetatch.Visibility = Visibility.Collapsed;
                    btnDebugView.Visibility = Visibility.Collapsed;
                    guitarPreview.Visibility = Visibility.Collapsed;
                    virtualOutputPanel.Visibility = Visibility.Collapsed;
                    break;

                case DeviceState.Discovered:
                    btnIdentify.IsEnabled = true;
                    btnEditName.IsEnabled = true;
                    autoConnectNumber.IsEnabled = true;
                    btnXinput.IsEnabled = true;
                    btnDetatch.IsEnabled = false;
                    btnDetatch.Visibility = Visibility.Collapsed;
                    btnDebugView.Visibility = Visibility.Collapsed;
                    guitarPreview.Visibility = Visibility.Collapsed;
                    virtualOutputPanel.Visibility = Visibility.Collapsed;
                    break;

                case DeviceState.Connected_XInput:
                    btnIdentify.IsEnabled = true;
                    btnEditName.IsEnabled = true;
                    autoConnectNumber.IsEnabled = true;
                    btnXinput.IsEnabled = false;
                    btnDetatch.IsEnabled = true;
                    btnDetatch.Visibility = Visibility.Visible;
#if DEBUG
                    btnDebugView.Visibility = Visibility.Visible;
#else
                    btnDebugView.Visibility = Visibility.Collapsed;
#endif
                    if (device.Type == ControllerType.Guitar)
                    {
                        holder = null;
                        guitarPreview.Visibility = Visibility.Visible;
                        virtualOutputPanel.Visibility = Visibility.Visible;
                        outputModeSelector.SelectedIndex = (int)UserPrefs.Instance.virtualOutputMode;
                        previewModeSelector.SelectedIndex = (int)UserPrefs.Instance.guitarPreviewMode;
                        virtualOutputDiffText.Text = "Mismatch: n/a";
                        virtualSourceStateText.Text = "Source: n/a";
                        virtualReadbackStateText.Text = "Readback: n/a";

                        var connectResult = ConnectVirtualBackendForGuitar();
                        if (connectResult.IsError)
                        {
                            if (IsOptionalVirtualOutputFailure(connectResult.Error))
                            {
                                DisconnectVirtualBackend();
                                DispatcherQueue.TryEnqueue(async () =>
                                {
                                    await PromptForVirtualOutputDriverAsync(connectResult.Error);
                                });
                            }
                            else
                            {
                                DispatcherQueue.TryEnqueue(async () =>
                                {
                                    Detatch();
                                    await ShowVirtualControllerErrorAsync(connectResult.Error);
                                    await PromptForVirtualOutputDriverAsync(connectResult.Error);
                                });
                                break;
                            }
                        }
                    }
                    else
                    {
                        virtualOutputPanel.Visibility = Visibility.Collapsed;
                        guitarPreview.Visibility = Visibility.Collapsed;
                        var xHolder = new Holders.XInputHolder(device.Type);
                        LoadProfile(properties.profile, xHolder);
                        var connectResult = xHolder.TryConnectXInput(targetXDevice);
                        if (connectResult.IsError)
                        {
                            holder = null;
                            DispatcherQueue.TryEnqueue(async () =>
                            {
                                Detatch();
                                await ShowVirtualControllerErrorAsync(connectResult.Error);
                                if (connectResult.Error.Kind == VirtualControllerErrorKind.DriverNotReady)
                                    await VirtualControllerDriverPrompt.PromptInstallAsync(this.XamlRoot);
                            });
                            break;
                        }
                        holder = xHolder;
                    }

                    device.SetPlayerLED(targetXDevice);
                    updateTimer = new System.Threading.Timer(HolderUpdate, device, 1000, UPDATE_SPEED);
                    break;
            }
        }

        void device_ExtensionChange(object? sender, NintrollerExtensionEventArgs e)
        {
            DeviceType = e.controllerType;
            if (holder != null) holder.AddMapping(DeviceType);
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateIcon(DeviceType);
                int playerNum = targetXDevice;
                Detatch();
                AssignToXinputPlayer(playerNum);
                UpdateIcon(DeviceType);
            });
        }

        void device_LowBattery(object? sender, LowBatteryEventArgs e)
        {
            SetBatteryStatus(e.batteryLevel == BatteryStatus.Low || e.batteryLevel == BatteryStatus.VeryLow);
        }

        void device_StateChange(object? sender, NintrollerStateEventArgs e)
        {
            if (updateTimer != null) updateTimer.Change(1000, UPDATE_SPEED);
            if (holder == null && virtualBackend == null) return;
            RumbleStep();
            var inputHolder = holder;
            inputHolder?.ClearAllValues();
            switch (e.controllerType)
            {
                case ControllerType.ProController:
                    #region Pro Controller
                    ProController pro = (ProController)e.state;
                    inputHolder?.SetValue(Inputs.ProController.A, pro.A); inputHolder?.SetValue(Inputs.ProController.B, pro.B);
                    inputHolder?.SetValue(Inputs.ProController.X, pro.X); inputHolder?.SetValue(Inputs.ProController.Y, pro.Y);
                    inputHolder?.SetValue(Inputs.ProController.UP, pro.Up); inputHolder?.SetValue(Inputs.ProController.DOWN, pro.Down);
                    inputHolder?.SetValue(Inputs.ProController.LEFT, pro.Left); inputHolder?.SetValue(Inputs.ProController.RIGHT, pro.Right);
                    inputHolder?.SetValue(Inputs.ProController.L, pro.L); inputHolder?.SetValue(Inputs.ProController.R, pro.R);
                    inputHolder?.SetValue(Inputs.ProController.ZL, pro.ZL); inputHolder?.SetValue(Inputs.ProController.ZR, pro.ZR);
                    inputHolder?.SetValue(Inputs.ProController.START, pro.Plus); inputHolder?.SetValue(Inputs.ProController.SELECT, pro.Minus);
                    inputHolder?.SetValue(Inputs.ProController.HOME, pro.Home); inputHolder?.SetValue(Inputs.ProController.LS, pro.LStick);
                    inputHolder?.SetValue(Inputs.ProController.RS, pro.RStick);
                    inputHolder?.SetValue(Inputs.ProController.LRIGHT, pro.LJoy.X > 0 ? pro.LJoy.X : 0f);
                    inputHolder?.SetValue(Inputs.ProController.LLEFT, pro.LJoy.X < 0 ? -pro.LJoy.X : 0f);
                    inputHolder?.SetValue(Inputs.ProController.LUP, pro.LJoy.Y > 0 ? pro.LJoy.Y : 0f);
                    inputHolder?.SetValue(Inputs.ProController.LDOWN, pro.LJoy.Y < 0 ? -pro.LJoy.Y : 0f);
                    inputHolder?.SetValue(Inputs.ProController.RRIGHT, pro.RJoy.X > 0 ? pro.RJoy.X : 0f);
                    inputHolder?.SetValue(Inputs.ProController.RLEFT, pro.RJoy.X < 0 ? -pro.RJoy.X : 0f);
                    inputHolder?.SetValue(Inputs.ProController.RUP, pro.RJoy.Y > 0 ? pro.RJoy.Y : 0f);
                    inputHolder?.SetValue(Inputs.ProController.RDOWN, pro.RJoy.Y < 0 ? -pro.RJoy.Y : 0f);
                    #endregion
                    break;
                case ControllerType.Wiimote: SetWiimoteInputs(((Wiimote)e.state)); break;
                case ControllerType.Nunchuk:
                case ControllerType.NunchukB:
                    #region Nunchuk
                    Nunchuk nun = (Nunchuk)e.state;
                    SetWiimoteInputs(nun.wiimote);
                    inputHolder?.SetValue(Inputs.Nunchuk.C, nun.C); inputHolder?.SetValue(Inputs.Nunchuk.Z, nun.Z);
                    inputHolder?.SetValue(Inputs.Nunchuk.RIGHT, nun.joystick.X > 0 ? nun.joystick.X : 0f);
                    inputHolder?.SetValue(Inputs.Nunchuk.LEFT, nun.joystick.X < 0 ? -nun.joystick.X : 0f);
                    inputHolder?.SetValue(Inputs.Nunchuk.UP, nun.joystick.Y > 0 ? nun.joystick.Y : 0f);
                    inputHolder?.SetValue(Inputs.Nunchuk.DOWN, nun.joystick.Y < 0 ? -nun.joystick.Y : 0f);
                    inputHolder?.SetValue(Inputs.Nunchuk.TILT_RIGHT, nun.accelerometer.X > 0 ? nun.accelerometer.X : 0f);
                    inputHolder?.SetValue(Inputs.Nunchuk.TILT_LEFT, nun.accelerometer.X < 0 ? -nun.accelerometer.X : 0f);
                    inputHolder?.SetValue(Inputs.Nunchuk.TILT_UP, nun.accelerometer.Y > 0 ? nun.accelerometer.Y : 0f);
                    inputHolder?.SetValue(Inputs.Nunchuk.TILT_DOWN, nun.accelerometer.Y < 0 ? -nun.accelerometer.Y : 0f);
                    inputHolder?.SetValue(Inputs.Nunchuk.ACC_SHAKE_X, nun.accelerometer.X > 1.15f);
                    inputHolder?.SetValue(Inputs.Nunchuk.ACC_SHAKE_Y, nun.accelerometer.Y > 1.15f);
                    inputHolder?.SetValue(Inputs.Nunchuk.ACC_SHAKE_Z, nun.accelerometer.Z > 1.15f);
                    #endregion
                    break;
                case ControllerType.ClassicController:
                    #region Classic Controller
                    ClassicController cc = (ClassicController)e.state;
                    SetWiimoteInputs(cc.wiimote);
                    inputHolder?.SetValue(Inputs.ClassicController.A, cc.A); inputHolder?.SetValue(Inputs.ClassicController.B, cc.B);
                    inputHolder?.SetValue(Inputs.ClassicController.X, cc.X); inputHolder?.SetValue(Inputs.ClassicController.Y, cc.Y);
                    inputHolder?.SetValue(Inputs.ClassicController.UP, cc.Up); inputHolder?.SetValue(Inputs.ClassicController.DOWN, cc.Down);
                    inputHolder?.SetValue(Inputs.ClassicController.LEFT, cc.Left); inputHolder?.SetValue(Inputs.ClassicController.RIGHT, cc.Right);
                    inputHolder?.SetValue(Inputs.ClassicController.L, cc.L.value > 0); inputHolder?.SetValue(Inputs.ClassicController.R, cc.R.value > 0);
                    inputHolder?.SetValue(Inputs.ClassicController.ZL, cc.ZL); inputHolder?.SetValue(Inputs.ClassicController.ZR, cc.ZR);
                    inputHolder?.SetValue(Inputs.ClassicController.START, cc.Start); inputHolder?.SetValue(Inputs.ClassicController.SELECT, cc.Select);
                    inputHolder?.SetValue(Inputs.ClassicController.HOME, cc.Home);
                    inputHolder?.SetValue(Inputs.ClassicController.LFULL, cc.LFull); inputHolder?.SetValue(Inputs.ClassicController.RFULL, cc.RFull);
                    inputHolder?.SetValue(Inputs.ClassicController.LT, cc.L.value > 0.1f ? cc.L.value : 0f);
                    inputHolder?.SetValue(Inputs.ClassicController.RT, cc.R.value > 0.1f ? cc.R.value : 0f);
                    inputHolder?.SetValue(Inputs.ClassicController.LRIGHT, cc.LJoy.X > 0 ? cc.LJoy.X : 0f);
                    inputHolder?.SetValue(Inputs.ClassicController.LLEFT, cc.LJoy.X < 0 ? -cc.LJoy.X : 0f);
                    inputHolder?.SetValue(Inputs.ClassicController.LUP, cc.LJoy.Y > 0 ? cc.LJoy.Y : 0f);
                    inputHolder?.SetValue(Inputs.ClassicController.LDOWN, cc.LJoy.Y < 0 ? -cc.LJoy.Y : 0f);
                    inputHolder?.SetValue(Inputs.ClassicController.RRIGHT, cc.RJoy.X > 0 ? cc.RJoy.X : 0f);
                    inputHolder?.SetValue(Inputs.ClassicController.RLEFT, cc.RJoy.X < 0 ? -cc.RJoy.X : 0f);
                    inputHolder?.SetValue(Inputs.ClassicController.RUP, cc.RJoy.Y > 0 ? cc.RJoy.Y : 0f);
                    inputHolder?.SetValue(Inputs.ClassicController.RDOWN, cc.RJoy.Y < 0 ? -cc.RJoy.Y : 0f);
                    #endregion
                    break;
                case ControllerType.ClassicControllerPro:
                    #region Classic Controller Pro
                    ClassicControllerPro ccp = (ClassicControllerPro)e.state;
                    SetWiimoteInputs(ccp.wiimote);
                    inputHolder?.SetValue(Inputs.ClassicControllerPro.A, ccp.A); inputHolder?.SetValue(Inputs.ClassicControllerPro.B, ccp.B);
                    inputHolder?.SetValue(Inputs.ClassicControllerPro.X, ccp.X); inputHolder?.SetValue(Inputs.ClassicControllerPro.Y, ccp.Y);
                    inputHolder?.SetValue(Inputs.ClassicControllerPro.UP, ccp.Up); inputHolder?.SetValue(Inputs.ClassicControllerPro.DOWN, ccp.Down);
                    inputHolder?.SetValue(Inputs.ClassicControllerPro.LEFT, ccp.Left); inputHolder?.SetValue(Inputs.ClassicControllerPro.RIGHT, ccp.Right);
                    inputHolder?.SetValue(Inputs.ClassicControllerPro.L, ccp.L); inputHolder?.SetValue(Inputs.ClassicControllerPro.R, ccp.R);
                    inputHolder?.SetValue(Inputs.ClassicControllerPro.ZL, ccp.ZL); inputHolder?.SetValue(Inputs.ClassicControllerPro.ZR, ccp.ZR);
                    inputHolder?.SetValue(Inputs.ClassicControllerPro.START, ccp.Start); inputHolder?.SetValue(Inputs.ClassicControllerPro.SELECT, ccp.Select);
                    inputHolder?.SetValue(Inputs.ClassicControllerPro.HOME, ccp.Home);
                    inputHolder?.SetValue(Inputs.ClassicControllerPro.LRIGHT, ccp.LJoy.X > 0 ? ccp.LJoy.X : 0f);
                    inputHolder?.SetValue(Inputs.ClassicControllerPro.LLEFT, ccp.LJoy.X < 0 ? -ccp.LJoy.X : 0f);
                    inputHolder?.SetValue(Inputs.ClassicControllerPro.LUP, ccp.LJoy.Y > 0 ? ccp.LJoy.Y : 0f);
                    inputHolder?.SetValue(Inputs.ClassicControllerPro.LDOWN, ccp.LJoy.Y < 0 ? -ccp.LJoy.Y : 0f);
                    inputHolder?.SetValue(Inputs.ClassicControllerPro.RRIGHT, ccp.RJoy.X > 0 ? ccp.RJoy.X : 0f);
                    inputHolder?.SetValue(Inputs.ClassicControllerPro.RLEFT, ccp.RJoy.X < 0 ? -ccp.RJoy.X : 0f);
                    inputHolder?.SetValue(Inputs.ClassicControllerPro.RUP, ccp.RJoy.Y > 0 ? ccp.RJoy.Y : 0f);
                    inputHolder?.SetValue(Inputs.ClassicControllerPro.RDOWN, ccp.RJoy.Y < 0 ? -ccp.RJoy.Y : 0f);
                    #endregion
                    break;
                case ControllerType.Guitar:
                    #region Wii Guitar
                    WiiGuitar wgt = (WiiGuitar)e.state;
                    inputHolder?.SetValue(Inputs.WiiGuitar.G, wgt.G); inputHolder?.SetValue(Inputs.WiiGuitar.R, wgt.R);
                    inputHolder?.SetValue(Inputs.WiiGuitar.Y, wgt.Y); inputHolder?.SetValue(Inputs.WiiGuitar.B, wgt.B);
                    inputHolder?.SetValue(Inputs.WiiGuitar.O, wgt.O);
                    inputHolder?.SetValue(Inputs.WiiGuitar.UP, wgt.Up); inputHolder?.SetValue(Inputs.WiiGuitar.DOWN, wgt.Down);
                    inputHolder?.SetValue(Inputs.WiiGuitar.LEFT, wgt.Left); inputHolder?.SetValue(Inputs.WiiGuitar.RIGHT, wgt.Right);
                    inputHolder?.SetValue(Inputs.WiiGuitar.WHAMMYHIGH, wgt.WhammyHigh); inputHolder?.SetValue(Inputs.WiiGuitar.WHAMMYLOW, wgt.WhammyLow);
                    inputHolder?.SetValue(Inputs.WiiGuitar.TILTHIGH, wgt.TiltHigh); inputHolder?.SetValue(Inputs.WiiGuitar.TILTLOW, wgt.TiltLow);
                    inputHolder?.SetValue(Inputs.WiiGuitar.START, wgt.Start); inputHolder?.SetValue(Inputs.WiiGuitar.SELECT, wgt.Select);

                    lastSourceOutput = ControllerOutputState.FromWiiGuitar(wgt);
                    if (virtualBackend != null)
                    {
                        var writeResult = virtualBackend.Update(lastSourceOutput);
                        if (writeResult.IsError)
                        {
                            if (ShouldKeepVirtualOutputErrorInline(writeResult.Error))
                            {
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    virtualOutputStatusText.Text = $"Virtual output: {virtualBackend.DisplayName} ({writeResult.Error.Message})";
                                });
                            }
                            else if (!backendWriteFailed)
                            {
                                backendWriteFailed = true;
                                var errorToShow = writeResult.Error;
                                _ = DispatcherQueue.TryEnqueue(async () => 
                                {
                                    await ShowVirtualControllerErrorAsync(errorToShow);
                                });
                            }
                        }
                        else
                        {
                            backendWriteFailed = false;
                        }
                    }
                    DispatcherQueue.TryEnqueue(UpdateGuitarPreviewPanel);
                    #endregion
                    break;
                case ControllerType.Drums:
                    #region Wii Drums
                    WiiDrums wdr = (WiiDrums)e.state;
                    inputHolder?.SetValue(Inputs.WiiDrums.G, wdr.G); inputHolder?.SetValue(Inputs.WiiDrums.R, wdr.R);
                    inputHolder?.SetValue(Inputs.WiiDrums.Y, wdr.Y); inputHolder?.SetValue(Inputs.WiiDrums.B, wdr.B);
                    inputHolder?.SetValue(Inputs.WiiDrums.O, wdr.O); inputHolder?.SetValue(Inputs.WiiDrums.BASS, wdr.Bass);
                    inputHolder?.SetValue(Inputs.WiiDrums.UP, wdr.Up); inputHolder?.SetValue(Inputs.WiiDrums.DOWN, wdr.Down);
                    inputHolder?.SetValue(Inputs.WiiDrums.LEFT, wdr.Left); inputHolder?.SetValue(Inputs.WiiDrums.RIGHT, wdr.Right);
                    inputHolder?.SetValue(Inputs.WiiDrums.START, wdr.Start); inputHolder?.SetValue(Inputs.WiiDrums.SELECT, wdr.Select);
                    #endregion
                    break;
            }
            inputHolder?.Update();
            if (updateTimer != null) updateTimer.Change(100, UPDATE_SPEED);
        }

        private void device_Disconnected(object? sender, DisconnectedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                Detatch();
                OnConnectionLost?.Invoke(this);
                MainWindow.Instance?.ShowBalloon("Connection Lost",
                    "Failed to communicate with controller. It may no longer be connected.", 2);
            });
        }

        private void SetWiimoteInputs(Wiimote wm)
        {
            if (holder == null) return;

            wm.irSensor.Normalize();
            holder.SetValue(Inputs.Wiimote.A, wm.buttons.A); holder.SetValue(Inputs.Wiimote.B, wm.buttons.B);
            holder.SetValue(Inputs.Wiimote.ONE, wm.buttons.One); holder.SetValue(Inputs.Wiimote.TWO, wm.buttons.Two);
            holder.SetValue(Inputs.Wiimote.UP, wm.buttons.Up); holder.SetValue(Inputs.Wiimote.DOWN, wm.buttons.Down);
            holder.SetValue(Inputs.Wiimote.LEFT, wm.buttons.Left); holder.SetValue(Inputs.Wiimote.RIGHT, wm.buttons.Right);
            holder.SetValue(Inputs.Wiimote.MINUS, wm.buttons.Minus); holder.SetValue(Inputs.Wiimote.PLUS, wm.buttons.Plus);
            holder.SetValue(Inputs.Wiimote.HOME, wm.buttons.Home);
            holder.SetValue(Inputs.Wiimote.TILT_RIGHT, wm.accelerometer.X > 0 ? wm.accelerometer.X : 0);
            holder.SetValue(Inputs.Wiimote.TILT_LEFT, wm.accelerometer.X < 0 ? wm.accelerometer.X : 0);
            holder.SetValue(Inputs.Wiimote.TILT_UP, wm.accelerometer.Y > 0 ? wm.accelerometer.Y : 0);
            holder.SetValue(Inputs.Wiimote.TILT_DOWN, wm.accelerometer.Y < 0 ? wm.accelerometer.Y : 0);
            holder.SetValue(Inputs.Wiimote.ACC_SHAKE_X, wm.accelerometer.X > 1.15);
            holder.SetValue(Inputs.Wiimote.ACC_SHAKE_Y, wm.accelerometer.Y > 1.15);
            holder.SetValue(Inputs.Wiimote.ACC_SHAKE_Z, wm.accelerometer.Z > 1.15);
            if (snapIRpointer && !wm.irSensor.point1.visible && !wm.irSensor.point2.visible)
            {
                if (properties.pointerMode == Property.PointerOffScreenMode.SnapX ||
                    properties.pointerMode == Property.PointerOffScreenMode.SnapXY)
                    wm.irSensor.X = previousIR.X;
                if (properties.pointerMode == Property.PointerOffScreenMode.SnapY ||
                    properties.pointerMode == Property.PointerOffScreenMode.SnapXY)
                    wm.irSensor.Y = previousIR.Y;
            }
            holder.SetValue(Inputs.Wiimote.IR_RIGHT, wm.irSensor.X > 0 ? wm.irSensor.X : 0);
            holder.SetValue(Inputs.Wiimote.IR_LEFT, wm.irSensor.X < 0 ? wm.irSensor.X : 0);
            holder.SetValue(Inputs.Wiimote.IR_UP, wm.irSensor.Y > 0 ? wm.irSensor.Y : 0);
            holder.SetValue(Inputs.Wiimote.IR_DOWN, wm.irSensor.Y < 0 ? wm.irSensor.Y : 0);
            previousIR = wm.irSensor;
        }

        private void UpdateGuitarPreviewPanel()
        {
            guitarPreview.Visibility = Visibility.Visible;
            ControllerOutputState previewState = lastSourceOutput;

            if (UserPrefs.Instance.guitarPreviewMode == GuitarPreviewMode.VirtualOutput)
            {
                var readResult = TryReadVirtualOutputState();
                if (readResult.IsOk)
                {
                    previewState = readResult.Value;
                    lastReadbackOutput = previewState;
                }
                else
                {
                    virtualReadbackStateText.Text = "Readback: unavailable";
                    virtualOutputDiffText.Text = $"Output preview unavailable: {readResult.Error.Message}";
                }
            }
            else
            {
                lastReadbackOutput = ControllerOutputState.Empty;
                virtualReadbackStateText.Text = "Readback: disabled";
            }

            UpdateGuitarPreview(previewState);
            virtualSourceStateText.Text = $"Source: {lastSourceOutput.ToCompactDebugString()}";
            virtualReadbackStateText.Text = lastReadbackOutput == ControllerOutputState.Empty
                ? virtualReadbackStateText.Text
                : $"Readback: {lastReadbackOutput.ToCompactDebugString()}";
            virtualOutputDiffText.Text = BuildMismatchSummary(lastSourceOutput, lastReadbackOutput);
        }

        private void UpdateGuitarPreview(ControllerOutputState state)
        {
            SetFretPreview(fretG, state.Green, FretGBrush);
            SetFretPreview(fretR, state.Red, FretRBrush);
            SetFretPreview(fretY, state.Yellow, FretYBrush);
            SetFretPreview(fretB, state.Blue, FretBBrush);
            SetFretPreview(fretO, state.Orange, FretOBrush);
            strumIndicator.Text = GetStrumPreview(state);

            SetTextPreview(previewWH, state.Whammy > 0.05f);
            SetTextPreview(previewWL, state.Whammy < -0.05f);
            SetTextPreview(previewTH, state.Tilt > 0.05f);
            SetTextPreview(previewTL, state.Tilt < -0.05f);
            SetTextPreview(previewStart, state.Start);
            SetTextPreview(previewSelect, state.Select);
        }

        private static void SetFretPreview(Microsoft.UI.Xaml.Controls.Border fret, bool pressed, SolidColorBrush activeBrush)
        {
            fret.Background = pressed ? activeBrush : PreviewInactiveBrush;
        }

        private static void SetTextPreview(TextBlock textBlock, bool active)
        {
            textBlock.Foreground = active ? PreviewTextActiveBrush : PreviewTextInactiveBrush;
            textBlock.FontWeight = active
                ? Microsoft.UI.Text.FontWeights.SemiBold
                : Microsoft.UI.Text.FontWeights.Normal;
        }

        private static string GetStrumPreview(ControllerOutputState state)
        {
            if (state.StrumUp && state.DPadRight) return "↗";
            if (state.StrumUp && state.DPadLeft) return "↖";
            if (state.StrumDown && state.DPadRight) return "↘";
            if (state.StrumDown && state.DPadLeft) return "↙";
            if (state.StrumUp) return "↑";
            if (state.StrumDown) return "↓";
            if (state.DPadLeft) return "←";
            if (state.DPadRight) return "→";
            return "•";
        }

        private Result<Unit, VirtualControllerError> ConnectVirtualBackendForGuitar()
        {
            DisconnectVirtualBackend();
            backendWriteFailed = false;
            lastReadbackOutput = ControllerOutputState.Empty;

            VirtualOutputMode selectedMode = UserPrefs.Instance.virtualOutputMode;
            virtualBackend = VirtualControllerBackendFactory.Create(selectedMode);
            if (selectedMode == VirtualOutputMode.HidMaestroExperimental
                && UserPrefs.Instance.guitarPreviewMode != GuitarPreviewMode.VirtualOutput)
            {
                UserPrefs.Instance.guitarPreviewMode = GuitarPreviewMode.VirtualOutput;
                var saveResult = UserPrefs.SavePrefs();
                if (saveResult.IsError)
                    System.Diagnostics.Debug.WriteLine(saveResult.Error.ToDisplayString());

                previewModeSelector.SelectionChanged -= previewModeSelector_SelectionChanged;
                previewModeSelector.SelectedIndex = (int)GuitarPreviewMode.VirtualOutput;
                previewModeSelector.SelectionChanged += previewModeSelector_SelectionChanged;
            }

            int targetId = selectedMode == VirtualOutputMode.VJoyExperimental
                ? Math.Max(1, UserPrefs.Instance.vJoyDeviceId)
                : targetXDevice;

            var connectResult = virtualBackend.Connect(targetId, ControllerType.Guitar);
            if (connectResult.IsError)
            {
                virtualOutputStatusText.Text = $"Virtual output: {virtualBackend.DisplayName} (failed)";
                virtualBackend.Dispose();
                virtualBackend = null;
                return connectResult;
            }

            var identityResult = virtualBackend.GetIdentity();
            if (identityResult.IsOk)
            {
                virtualIdentity = identityResult.Value;
                virtualOutputStatusText.Text = $"Virtual output: {virtualIdentity.ToCompactDisplayString()}";
            }
            else
            {
                virtualOutputStatusText.Text = $"Virtual output: {virtualBackend.DisplayName}";
            }

            var readbackResult = virtualBackend.CreateReadback();
            if (readbackResult.IsOk)
            {
                virtualReadback = readbackResult.Value;
                if (virtualIdentity != null)
                {
                    var attachResult = virtualReadback.Attach(virtualIdentity);
                    if (attachResult.IsError)
                    {
                        virtualReadback.Dispose();
                        virtualReadback = null;
                    }
                }

                if (virtualReadback != null)
                    virtualReadbackStateText.Text = "Readback: connected";
            }

            if (virtualReadback == null)
            {
                virtualOutputDiffText.Text = "Output preview unavailable";
                virtualReadbackStateText.Text = "Readback: unavailable";
            }

            return Result<Unit, VirtualControllerError>.Ok(Unit.Value);
        }

        private Result<ControllerOutputState, VirtualControllerError> TryReadVirtualOutputState()
        {
            if (virtualReadback == null)
            {
                return Result<ControllerOutputState, VirtualControllerError>.Err(
                    VirtualControllerError.DriverNotReady("Output preview unavailable for the selected backend."));
            }

            try
            {
                return virtualReadback.ReadState();
            }
            catch (Exception ex)
            {
                return Result<ControllerOutputState, VirtualControllerError>.Err(
                    VirtualControllerError.WriteFailed($"Readback failed: {ex.Message}"));
            }
        }

        private static string BuildMismatchSummary(ControllerOutputState source, ControllerOutputState output)
        {
            if (output == ControllerOutputState.Empty)
                return "Mismatch: n/a";

            var mismatch = new List<string>(14);
            bool skipAnalogReadbackMismatch = UserPrefs.Instance.virtualOutputMode == VirtualOutputMode.HidMaestroExperimental;
            if (source.Green != output.Green) mismatch.Add("G");
            if (source.Red != output.Red) mismatch.Add("R");
            if (source.Yellow != output.Yellow) mismatch.Add("Y");
            if (source.Blue != output.Blue) mismatch.Add("B");
            if (source.Orange != output.Orange) mismatch.Add("O");
            if (source.StrumUp != output.StrumUp) mismatch.Add("SU");
            if (source.StrumDown != output.StrumDown) mismatch.Add("SD");
            if (source.DPadLeft != output.DPadLeft) mismatch.Add("DL");
            if (source.DPadRight != output.DPadRight) mismatch.Add("DR");
            if (source.Start != output.Start) mismatch.Add("St");
            if (source.Select != output.Select) mismatch.Add("Sl");
            if (source.Home != output.Home) mismatch.Add("Hm");
            if (!skipAnalogReadbackMismatch && Math.Abs(source.Whammy - output.Whammy) > 0.12f) mismatch.Add("Wh");
            if (!skipAnalogReadbackMismatch && Math.Abs(source.Tilt - output.Tilt) > 0.12f) mismatch.Add("Tl");

            if (mismatch.Count == 0)
                return "Mismatch: none";

            return $"Mismatch: {string.Join(", ", mismatch)}";
        }

        private void DisconnectVirtualBackend()
        {
            if (virtualReadback != null)
            {
                virtualReadback.Dispose();
                virtualReadback = null;
            }

            if (virtualBackend != null)
            {
                virtualBackend.Disconnect();
                virtualBackend.Dispose();
                virtualBackend = null;
            }

            virtualIdentity = null;
            lastReadbackOutput = ControllerOutputState.Empty;
            backendWriteFailed = false;
            if (virtualOutputStatusText != null)
                virtualOutputStatusText.Text = "Virtual output: not connected";
            if (virtualSourceStateText != null)
                virtualSourceStateText.Text = "Source: n/a";
            if (virtualReadbackStateText != null)
                virtualReadbackStateText.Text = "Readback: n/a";
        }

        private async System.Threading.Tasks.Task ShowVirtualControllerErrorAsync(VirtualControllerError error)
        {
            if (error.Kind == VirtualControllerErrorKind.DriverNotReady
                && (UserPrefs.Instance.virtualOutputMode == VirtualOutputMode.ScpXbox360
                    || UserPrefs.Instance.virtualOutputMode == VirtualOutputMode.HidMaestroExperimental
                    || UserPrefs.Instance.virtualOutputMode == VirtualOutputMode.VJoyExperimental))
                return;

            if (this.XamlRoot == null)
                return;

            var dlg = new ContentDialog
            {
                Title = "Virtual Output Failed",
                Content = error.ToDisplayString(),
                PrimaryButtonText = "OK",
                XamlRoot = this.XamlRoot
            };

            await VirtualControllerDriverPrompt.ShowDialogAsync(dlg);
        }

        private async System.Threading.Tasks.Task PromptForVirtualOutputDriverAsync(VirtualControllerError error)
        {
            if (error.Kind != VirtualControllerErrorKind.DriverNotReady)
                return;

            switch (UserPrefs.Instance.virtualOutputMode)
            {
                case VirtualOutputMode.ScpXbox360:
                    await VirtualControllerDriverPrompt.PromptInstallAsync(this.XamlRoot);
                    break;

                case VirtualOutputMode.VJoyExperimental:
                    await VirtualControllerDriverPrompt.PromptVJoyInstallAsync(this.XamlRoot);
                    break;

                case VirtualOutputMode.HidMaestroExperimental:
                    await VirtualControllerDriverPrompt.PromptHidMaestroInstallAsync(this.XamlRoot);
                    break;
            }
        }

        private static bool IsOptionalVirtualOutputFailure(VirtualControllerError error)
        {
            return (UserPrefs.Instance.virtualOutputMode == VirtualOutputMode.HidMaestroExperimental
                    || UserPrefs.Instance.virtualOutputMode == VirtualOutputMode.VJoyExperimental)
                && error.Kind == VirtualControllerErrorKind.DriverNotReady;
        }

        private static bool ShouldKeepVirtualOutputErrorInline(VirtualControllerError error)
        {
            return UserPrefs.Instance.virtualOutputMode == VirtualOutputMode.HidMaestroExperimental
                && (error.Kind == VirtualControllerErrorKind.ConnectionFailed
                    || error.Kind == VirtualControllerErrorKind.WriteFailed);
        }

        private void HolderUpdate(object? holderState)
        {
            holder?.Update();
            RumbleStep();
            SetBatteryStatus(device.BatteryLevel == BatteryStatus.Low);
        }

        void RumbleStep()
        {
            if (holder == null && virtualBackend == null) return;

            if (identifying) return;
            bool cur = device.RumbleEnabled;
            if (!properties.useRumble) { if (cur) device.RumbleEnabled = false; return; }
            rumbleAmount = holder?.RumbleAmount ?? ((virtualBackend as ScpXInputBackend)?.LastRumbleAmount ?? 0);
            float modifier = properties.rumbleIntensity * 0.5f;
            float dutyCycle = rumbleAmount < 256
                ? rumbleSlowMult * rumbleAmount / 256f
                : rumbleAmount / 65535f;
            int stopStep = (int)Math.Round(modifier * dutyCycle * rumbleStepPeriod);
            if (rumbleStepCount < stopStep) { if (!cur) device.RumbleEnabled = true; }
            else { if (cur) device.RumbleEnabled = false; }
            if (++rumbleStepCount >= rumbleStepPeriod) rumbleStepCount = 0;
        }

        private void SetBatteryStatus(bool isLow)
        {
            if (isLow && !lowBatteryFired)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    statusGradient.Background = new SolidColorBrush(global::Microsoft.UI.Colors.OrangeRed);
                    if (_trayService_IsVisible())
                    {
                        lowBatteryFired = true;
                        MainWindow.Instance?.ShowBalloon(
                            "Battery Low",
                            dName + (!dName.Equals(device.Type.ToString()) ? " (" + device.Type.ToString() + ") " : " ")
                                  + "is running low on battery life.",
                            2,
                            System.Media.SystemSounds.Hand);
                    }
                });
            }
            else if (!isLow && lowBatteryFired)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    statusGradient.Background = new SolidColorBrush(global::Microsoft.UI.Colors.Transparent);
                    lowBatteryFired = false;
                });
            }
        }

        // Helper: tray visible check avoids dependency on the tray field
        private static bool _trayService_IsVisible() => MainWindow.Instance != null;

        private void LoadProfile(string profilePath, Holders.Holder h)
        {
            Profile? loadedProfile = null;
            var profileResult = TryLoadProfile(profilePath);
            if (profileResult.IsOk)
            {
                loadedProfile = profileResult.Value;
            }
            else if (!string.IsNullOrWhiteSpace(profilePath))
            {
                System.Diagnostics.Debug.WriteLine(profileResult.Error.ToDisplayString());
            }

            if (loadedProfile == null) loadedProfile = UserPrefs.Instance.defaultProfile;
            if (loadedProfile != null)
            {
                for (int i = 0; i < Math.Min(loadedProfile.controllerMapKeys.Count, loadedProfile.controllerMapValues.Count); i++)
                {
                    h.SetMapping(loadedProfile.controllerMapKeys[i], loadedProfile.controllerMapValues[i]);
                    CheckIR(loadedProfile.controllerMapKeys[i]);
                }
            }
        }

        private static Result<Profile, PreferencesError> TryLoadProfile(string profilePath)
        {
            if (string.IsNullOrWhiteSpace(profilePath))
                return Result<Profile, PreferencesError>.Err(
                    PreferencesError.ValidationFailed("Profile path is empty."));

            if (!File.Exists(profilePath))
                return Result<Profile, PreferencesError>.Err(
                    PreferencesError.FileNotFound(profilePath));

            var serializer = new XmlSerializer(typeof(Profile));
            try
            {
                using var stream = File.OpenRead(profilePath);
                using var reader = new StreamReader(stream);
                var profile = serializer.Deserialize(reader) as Profile;
                if (profile == null)
                {
                    return Result<Profile, PreferencesError>.Err(
                        PreferencesError.InvalidXml(profilePath, new InvalidOperationException("Profile XML did not deserialize to a valid profile.")));
                }

                return Result<Profile, PreferencesError>.Ok(profile);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Result<Profile, PreferencesError>.Err(PreferencesError.AccessDenied(profilePath, ex));
            }
            catch (System.Xml.XmlException ex)
            {
                return Result<Profile, PreferencesError>.Err(PreferencesError.InvalidXml(profilePath, ex));
            }
            catch (InvalidOperationException ex) when (ex.InnerException is System.Xml.XmlException)
            {
                return Result<Profile, PreferencesError>.Err(PreferencesError.InvalidXml(profilePath, ex));
            }
            catch (InvalidOperationException ex)
            {
                return Result<Profile, PreferencesError>.Err(PreferencesError.Unknown(profilePath, ex));
            }
            catch (System.Security.SecurityException ex)
            {
                return Result<Profile, PreferencesError>.Err(PreferencesError.AccessDenied(profilePath, ex));
            }
            catch (IOException ex)
            {
                return Result<Profile, PreferencesError>.Err(PreferencesError.Unknown(profilePath, ex));
            }
        }

        private void UpdateIcon(ControllerType cType)
        {
            string key = cType switch
            {
                ControllerType.ProController => "ProIcon",
                ControllerType.ClassicControllerPro => "CCPIcon",
                ControllerType.ClassicController => "CCIcon",
                ControllerType.Nunchuk => "WNIcon",
                ControllerType.NunchukB => "WNIcon",
                ControllerType.Guitar => "WGTIcon",
                ControllerType.Drums => "WDRIcon",
                _ => "WIcon"
            };
            icon.Source = Application.Current.Resources[key] as Microsoft.UI.Xaml.Media.ImageSource;
            UserPrefs.Instance.UpdateDeviceIcon(devicePath, key);
        }

        private void ApplyCalibration(Property.CalibrationPreference calPref, string calString)
        {
            switch (calPref)
            {
                case Property.CalibrationPreference.Default: device.SetCalibration(Calibrations.CalibrationPreset.Default); break;
                case Property.CalibrationPreference.More: device.SetCalibration(Calibrations.CalibrationPreset.Modest); break;
                case Property.CalibrationPreference.Extra: device.SetCalibration(Calibrations.CalibrationPreset.Extra); break;
                case Property.CalibrationPreference.Minimal: device.SetCalibration(Calibrations.CalibrationPreset.Minimum); break;
                case Property.CalibrationPreference.Raw: device.SetCalibration(Calibrations.CalibrationPreset.None); break;
                case Property.CalibrationPreference.Custom:
                    var cs = new CalibrationStorage(calString);
                    device.SetCalibration(cs.ProCalibration); device.SetCalibration(cs.NunchukCalibration);
                    device.SetCalibration(cs.ClassicCalibration); device.SetCalibration(cs.ClassicProCalibration);
                    device.SetCalibration(cs.WiimoteCalibration);
                    break;
            }
        }

        private void CheckIR(string assignment)
        {
            if (assignment.StartsWith("wIR") && device != null && device.IRMode == IRCamMode.Off)
            {
                if (device.Type == ControllerType.Wiimote || device.Type == ControllerType.Nunchuk || device.Type == ControllerType.NunchukB)
                    device.IRMode = IRCamMode.Basic;
            }
        }

        #region UI Events

        private void icon_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout(icon);
        }

        private void XInputFlyout_Opening(object sender, object e)
        {
            XOption1.IsEnabled = Holders.XInputHolder.availabe[0];
            XOption2.IsEnabled = Holders.XInputHolder.availabe[1];
            XOption3.IsEnabled = Holders.XInputHolder.availabe[2];
            XOption4.IsEnabled = Holders.XInputHolder.availabe[3];
        }

        private void AssignToXinputPlayer(int player)
        {
            device.BeginReading();
            device.GetStatus();
            targetXDevice = player;
            ConnectionState = DeviceState.Connected_XInput;
            RefreshState();
        }

        private async void XOption_Click(object sender, RoutedEventArgs e)
        {
            var item = (MenuFlyoutItem)sender;
            if (Device.Type != ControllerType.ProController)
            {
                var dlg = new ContentDialog
                {
                    Title = "Connect Wii Remote",
                    Content = "Press 1+2 on the Wii remote and press OK to continue.",
                    PrimaryButtonText = "OK",
                    CloseButtonText = "Cancel",
                    XamlRoot = this.XamlRoot
                };
                if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
            }
            if (TryOpenDeviceStream())
            {
                if (int.TryParse(item.Name.Replace("XOption", ""), out int tmp))
                    AssignToXinputPlayer(tmp);
            }
        }

        private void typeOption_Click(object sender, RoutedEventArgs e)
        {
            var item = (MenuFlyoutItem)sender;
            ControllerType ct = item.Name switch
            {
                "typeClear" => ControllerType.Unknown,
                "typePro" => ControllerType.ProController,
                "typeWiimote" => ControllerType.Wiimote,
                "typeNunchuk" => ControllerType.Nunchuk,
                "typeClassic" => ControllerType.ClassicController,
                "typeClassicPro" => ControllerType.ClassicControllerPro,
                "typeGuitar" => ControllerType.Guitar,
                _ => ControllerType.Unknown
            };
            device.ForceControllerType(ct);
            RefreshState();
        }

        private void btnDetatch_Click(object sender, RoutedEventArgs e) => Detatch();

        private async void btnConfig_Click(object sender, RoutedEventArgs e)
        {
            if (holder == null) return;

            var config = new ControllerMappingWindow(holder.Mappings, device.Type);
            config.XamlRoot = this.XamlRoot;
            await config.ShowAsync();
            if (config.result)
            {
                foreach (var pair in config.map)
                {
                    holder.SetMapping(pair.Key, pair.Value);
                    CheckIR(pair.Key);
                }
            }
        }

        private void btnIdentify_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Start of btnIdentify_click");
            bool wasConnected = Connected;

            if (wasConnected || TryOpenDeviceStream())
            {
                if (!wasConnected) device.BeginReading();
                identifying = true;
                device.RumbleEnabled = true;
                Delay(500).ContinueWith(o => { identifying = false; device.RumbleEnabled = false; if (!wasConnected) device.StopReading(); });
                device.SetPlayerLED(1);
                const int L = 400;
                Delay((L / 7) * 1).ContinueWith(o => device.SetPlayerLED(2));
                Delay((L / 7) * 2).ContinueWith(o => device.SetPlayerLED(3));
                Delay((L / 7) * 3).ContinueWith(o => device.SetPlayerLED(4));
                Delay((L / 7) * 4).ContinueWith(o => device.SetPlayerLED(3));
                Delay((L / 7) * 5).ContinueWith(o => device.SetPlayerLED(2));
                Delay((L / 7) * 6).ContinueWith(o => device.SetPlayerLED(1));
                if (targetXDevice != 0) Delay(L).ContinueWith(o => device.SetPlayerLED(targetXDevice));
            }

            Console.WriteLine("end of btnIdentify_Click");
        }

        private bool TryOpenDeviceStream()
        {
            if (device.DataStream is not WinBtStream stream)
                return false;

            var openResult = stream.TryOpenConnection();
            if (openResult.IsError)
            {
                System.Diagnostics.Debug.WriteLine(openResult.Error.ToDisplayString());
                return false;
            }

            return device.DataStream.CanRead;
        }

        private void btnXinput_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            btnXinput.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void outputModeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (outputModeSelector.SelectedIndex < 0)
                return;

            UserPrefs.Instance.virtualOutputMode = (VirtualOutputMode)outputModeSelector.SelectedIndex;
            var saveResult = UserPrefs.SavePrefs();
            if (saveResult.IsError)
                System.Diagnostics.Debug.WriteLine(saveResult.Error.ToDisplayString());

            if (device.Type != ControllerType.Guitar || ConnectionState != DeviceState.Connected_XInput)
                return;

            var reconnect = ConnectVirtualBackendForGuitar();
            if (reconnect.IsError)
            {
                await ShowVirtualControllerErrorAsync(reconnect.Error);
                await PromptForVirtualOutputDriverAsync(reconnect.Error);
            }
        }

        private void previewModeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (previewModeSelector.SelectedIndex < 0)
                return;

            UserPrefs.Instance.guitarPreviewMode = (GuitarPreviewMode)previewModeSelector.SelectedIndex;
            var saveResult = UserPrefs.SavePrefs();
            if (saveResult.IsError)
                System.Diagnostics.Debug.WriteLine(saveResult.Error.ToDisplayString());

            if (device.Type == ControllerType.Guitar && ConnectionState == DeviceState.Connected_XInput)
                UpdateGuitarPreviewPanel();
        }

        private void btnEditName_Click(object sender, RoutedEventArgs e)
        {
            nameInput.Text = dName;
            labelName.Visibility = Visibility.Collapsed;
            nameInput.Visibility = Visibility.Visible;
            nameInput.Focus(FocusState.Programmatic);
            nameInput.SelectAll();
        }

        private void nameInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (properties != null)
                properties.name = nameInput.Text;
        }

        private void nameInput_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                CommitNameEdit();
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Escape)
            {
                nameInput.Text = dName;
                EndNameEdit();
                e.Handled = true;
            }
        }

        private void nameInput_LostFocus(object sender, RoutedEventArgs e) => CommitNameEdit();

        private void CommitNameEdit()
        {
            string name = string.IsNullOrWhiteSpace(nameInput.Text) ? device.Type.ToString() : nameInput.Text.Trim();
            properties.name = name;
            SetName(name);
            SaveDeviceProperties();
            EndNameEdit();
        }

        private void EndNameEdit()
        {
            nameInput.Visibility = Visibility.Collapsed;
            labelName.Visibility = Visibility.Visible;
        }

        private void AutoConnect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (properties == null) return;

            properties.autoConnect = autoConnectNumber.SelectedIndex > 0;
            properties.autoNum = autoConnectNumber.SelectedIndex;
            SaveDeviceProperties();
        }

        private void SaveDeviceProperties()
        {
            UserPrefs.Instance.AddDevicePref(properties);
            var saveResult = UserPrefs.SavePrefs();
            if (saveResult.IsError)
                System.Diagnostics.Debug.WriteLine(saveResult.Error.ToDisplayString());
        }

        private async void btnProperties_Click(object sender, RoutedEventArgs e)
        {
            var win = new PropWindow(properties, device.Type.ToString());
            win.XamlRoot = this.XamlRoot;
            await win.ShowAsync();

            if (win.customCalibrate)
            {
                var cb = new CalibrateWindow(device);
                cb.XamlRoot = this.XamlRoot;
                await cb.ShowAsync();
                if (cb.doSave)
                {
                    win.props.calString = cb.Calibration.ToString();
                    win.XamlRoot = this.XamlRoot;
                    await win.ShowAsync();
                }
            }

            if (win.doSave)
            {
                ApplyCalibration(win.props.calPref, win.props.calString);
                properties = new Property(win.props);
                snapIRpointer = properties.pointerMode != Property.PointerOffScreenMode.Center;
                SetName(properties.name);
                UserPrefs.Instance.AddDevicePref(properties);
                var saveResult = UserPrefs.SavePrefs();
                if (saveResult.IsError)
                    System.Diagnostics.Debug.WriteLine(saveResult.Error.ToDisplayString());
            }
        }

        private void btnDebugView_Click(object sender, RoutedEventArgs e)
        {
#if DEBUG
            DebugViewActivate();
#endif
        }

        #endregion

        static System.Threading.Tasks.Task Delay(int ms)
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<object?>();
            new System.Threading.Timer(_ => tcs.SetResult(null)).Change(ms, -1);
            return tcs.Task;
        }
    }
}
