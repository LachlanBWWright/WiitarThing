using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Shared.Windows;

namespace WiinUSoft.Windows
{
    public partial class SyncDialog : ContentDialog
    {
        private const byte ActiveInquiryTimeoutMultiplier = 4;

        public bool Cancelled { get; protected set; }
        public int Count { get; protected set; }

        private bool _notCompatable;
        private bool _startFired;
        private bool _scanRunning;
        private Task? _scanTask;
        private readonly HashSet<ulong> _notifiedRememberedDevices = new();

        public SyncDialog()
        {
            InitializeComponent();
        }

        public event EventHandler? NewDeviceFound;

        const int ERROR_SUCCESS                  = 0x00000000;
        const int ERROR_DEVICE_NOT_CONNECTED     = 0x0000048F;
        const int WAIT_TIMEOUT                   = 0x00000102;
        const int ERROR_GEN_FAILURE              = 0x0000001F;
        const int ERROR_NOT_AUTHENTICATED        = 0x000004DC;
        const int ERROR_NOT_ENOUGH_MEMORY        = 0x00000008;
        const int ERROR_REQ_NOT_ACCEP            = 0x00000047;
        const int ERROR_ACCESS_DENIED            = 0x00000005;
        const int ERROR_NOT_READY                = 0x00000015;
        const int ERROR_VC_DISCONNECTED          = 0x000000F0;
        const int ERROR_INVALID_PARAMETER        = 0x00000057;
        const int ERROR_SERVICE_DOES_NOT_EXIST   = 0x00000424;
        const int ERROR_NO_MORE_ITEMS            = 0x00000103;

        private static void PrepareRadioForPairing(IntPtr radio)
        {
            NativeImports.BluetoothEnableDiscovery(radio, true);
            NativeImports.BluetoothEnableIncomingConnections(radio, true);
        }

        private static void CloseDeviceFindHandle(IntPtr handle)
        {
            if (handle != IntPtr.Zero)
                NativeImports.BluetoothFindDeviceClose(handle);
        }

        private static bool TryActivateHidService(
            IntPtr radio,
            ref NativeImports.BLUETOOTH_DEVICE_INFO deviceInfo,
            ref Guid hidServiceClass,
            out uint serviceError,
            out uint activateError)
        {
            serviceError = 0;
            activateError = 0;

            uint serviceCount = 16;
            Guid[] services = new Guid[16];
            serviceError = NativeImports.BluetoothEnumerateInstalledServices(radio, ref deviceInfo, ref serviceCount, services);
            if (serviceError != 0)
                return false;

            activateError = NativeImports.BluetoothSetServiceState(radio, ref deviceInfo, ref hidServiceClass, 0x01);
            return activateError == 0;
        }

        static string GetBluetoothAuthenticationError(uint errCode)
        {
            return (int)errCode switch
            {
                ERROR_SUCCESS               => "Success.",
                ERROR_DEVICE_NOT_CONNECTED  => "Wiimote broke connection.",
                ERROR_GEN_FAILURE           => "Bluetooth Hardware Failure.",
                ERROR_NOT_AUTHENTICATED     => "Failed to authenticate. Wiimote rejected auto-generated PIN.",
                ERROR_NOT_ENOUGH_MEMORY     => "Not enough RAM to connect.",
                WAIT_TIMEOUT                => "Wiimote not responding to Bluetooth pair signal...",
                ERROR_REQ_NOT_ACCEP         => "Max number of Bluetooth connections for this adapter has already been reached.",
                ERROR_ACCESS_DENIED         => "Couldn't get permission to pair.",
                ERROR_NOT_READY             => "Unspecified error; Windows has refused to connect the Wiimote.",
                ERROR_VC_DISCONNECTED       => "Windows forced the connection to be dropped.",
                ERROR_NO_MORE_ITEMS         => "Be patient; Wiimote restarted the pairing process for some reason...",
                _                           => "(ERROR CODE 0x" + errCode.ToString("X", CultureInfo.InvariantCulture) + ")"
            };
        }

        protected void OnNewDeviceFound() => NewDeviceFound?.Invoke(this, EventArgs.Empty);

        private void ContentDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            if (_startFired) return;
            _startFired = true;
            _scanRunning = true;
            _scanTask = Task.Run(() => Sync());
        }

        private async void ContentDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (!_scanRunning) return;

            args.Cancel = true;
            var deferral = args.GetDeferral();
            try
            {
                if (!Cancelled)
                {
                    Prompt("Stopping scan...");
                    Cancelled = true;
                }

                if (_scanTask != null) await _scanTask;
                Hide();
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void ProcessDiscoveredWiiDevice(
            IntPtr radio,
            NativeImports.BLUETOOTH_RADIO_INFO radioInfo,
            ref NativeImports.BLUETOOTH_DEVICE_INFO deviceInfo,
            ref Guid hidServiceClass)
        {
            var rememberedButDisconnected = deviceInfo.fRemembered && !deviceInfo.fConnected;
            var str_fRemembered = deviceInfo.fRemembered && !rememberedButDisconnected
                ? ", but it is already synced!"
                : ". Attempting to pair now...";
            string label = deviceInfo.szName switch
            {
                "Nintendo RVL-CNT-01"    => "Found Wiimote",
                "Nintendo RVL-CNT-01-TR" => "Found 2nd-Gen Wiimote+",
                "Nintendo RVL-CNT-01-UC" => "Found Wii U Pro Controller",
                _                        => "Found Unknown Wii Device Type"
            };
            Prompt($"{label} (\"{deviceInfo.szName}\"){str_fRemembered}",
                isBold: !deviceInfo.fRemembered, isItalic: deviceInfo.fRemembered);

            if (deviceInfo.fRemembered && !rememberedButDisconnected)
            {
                Prompt("Windows already has this controller paired. Refreshing its HID service, then press the controller's buttons to reconnect.",
                    isItalic: true);

                if (TryActivateHidService(radio, ref deviceInfo, ref hidServiceClass, out uint serviceError, out uint activateError))
                {
                    Prompt("Controller service refreshed. Waiting for Windows to expose it as a HID device...",
                        isItalic: true);
                    Count += 1;
                    OnNewDeviceFound();
                }
                else
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Windows still has this controller paired, but refreshing its HID service failed.");
                    if (serviceError != 0) sb.AppendLine(" >>> SERVICE ERROR: " + new Win32Exception((int)serviceError).Message);
                    if (activateError != 0) sb.AppendLine(" >>> ACTIVATION ERROR: " + new Win32Exception((int)activateError).Message);
                    sb.AppendLine("Press the controller's buttons to reconnect, or use Remove All Wiimotes if the pairing is stale.");
                    Prompt(sb.ToString(), isBold: true, isItalic: true);

                    if (_notifiedRememberedDevices.Add(deviceInfo.Address))
                        OnNewDeviceFound();
                }

                return;
            }

            var password = new StringBuilder();
            bool success = true;
            var bytes = BitConverter.GetBytes(radioInfo.address);
            for (int i = 0; i < 6; i++) if (bytes[i] > 0) password.Append((char)bytes[i]);

            uint errForget = 0, errAuth = 0, errService = 0, errActivate = 0;

            if (rememberedButDisconnected)
            {
                Prompt("Windows has a stale pairing record for this controller. Removing it before reconnecting...",
                    isItalic: true);
                errForget = NativeImports.BluetoothRemoveDevice(ref deviceInfo.Address);
                success = errForget == 0;
            }

            if (success)
            {
                errAuth = NativeImports.BluetoothAuthenticateDevice(IntPtr.Zero, radio, ref deviceInfo, password.ToString(), 6);
                success = errAuth == 0;
            }

            if (!success)
            {
                var wiimoteBytes = BitConverter.GetBytes(deviceInfo.Address);
                password.Clear();
                for (int i = 0; i < 6; i++) if (wiimoteBytes[i] > 0) password.Append((char)wiimoteBytes[i]);
                errAuth = NativeImports.BluetoothAuthenticateDevice(IntPtr.Zero, radio, ref deviceInfo, password.ToString(), 6);
                success = errAuth == 0;
            }

            if (success)
            {
                success = TryActivateHidService(
                    radio,
                    ref deviceInfo,
                    ref hidServiceClass,
                    out errService,
                    out errActivate);
            }

            if (success)
            {
                Prompt("Successfully Paired!", isBold: true);
                Count += 1;
                OnNewDeviceFound();
            }
            else
            {
                var sb = new StringBuilder();
                if (errForget   != 0) sb.AppendLine(" >>> FAILED TO REMOVE: 0x" + errForget.ToString("X"));
                if (errAuth     != 0) sb.AppendLine(GetBluetoothAuthenticationError(errAuth));
                if (errService  != 0) sb.AppendLine(" >>> SERVICE ERROR: "    + new Win32Exception((int)errService).Message);
                if (errActivate != 0) sb.AppendLine(" >>> ACTIVATION ERROR: " + new Win32Exception((int)errActivate).Message);
                Prompt(sb.ToString(), isBold: true, isItalic: true);
            }
        }

        public static void RemoveAllWiimotes()
        {
            WiitarDebug.Log("FUNC BEGIN - RemoveAllWiimotes");
            var radioParams = new NativeImports.BLUETOOTH_FIND_RADIO_PARAMS();
            var btRadios  = new List<IntPtr>();
            IntPtr foundRadio;
            radioParams.Initialize();

            IntPtr foundResult = NativeImports.BluetoothFindFirstRadio(ref radioParams, out foundRadio);
            if (foundResult != IntPtr.Zero)
            {
                bool more;
                do
                {
                    if (foundRadio != IntPtr.Zero) btRadios.Add(foundRadio);
                    more = NativeImports.BluetoothFindNextRadio(foundResult, out foundRadio);
                } while (more);
                NativeImports.BluetoothFindRadioClose(foundResult);
            }

            if (btRadios.Count > 0)
            {
                foreach (var radio in btRadios)
                {
                    var radioInfo   = new NativeImports.BLUETOOTH_RADIO_INFO();
                    var deviceInfo  = new NativeImports.BLUETOOTH_DEVICE_INFO();
                    var searchParams= new NativeImports.BLUETOOTH_DEVICE_SEARCH_PARAMS();
                    radioInfo.Initialize(); deviceInfo.Initialize(); searchParams.Initialize();

                    uint getInfoError = NativeImports.BluetoothGetRadioInfo(radio, ref radioInfo);
                    if (getInfoError == 0)
                    {
                        PrepareRadioForPairing(radio);
                        searchParams.hRadio = radio;
                        searchParams.fIssueInquiry = true;
                        searchParams.fReturnUnknown = true;
                        searchParams.fReturnConnected = true;
                        searchParams.fReturnRemembered = true;
                        searchParams.fReturnAuthenticated = true;
                        searchParams.cTimeoutMultiplier = ActiveInquiryTimeoutMultiplier;

                        IntPtr found = NativeImports.BluetoothFindFirstDevice(ref searchParams, ref deviceInfo);
                        try
                        {
                            if (found != IntPtr.Zero)
                            {
                                do
                                {
                                    if (deviceInfo.szName.StartsWith("Nintendo RVL-CNT-01", StringComparison.Ordinal) &&
                                        (deviceInfo.fRemembered || deviceInfo.fConnected))
                                    {
                                        uint removeError = NativeImports.BluetoothRemoveDevice(ref deviceInfo.Address);
                                        if (removeError != 0)
                                            WiitarDebug.Log($"BluetoothRemoveDevice failed: 0x{removeError:X8}");
                                    }
                                } while (NativeImports.BluetoothFindNextDevice(found, ref deviceInfo));
                            }
                        }
                        finally
                        {
                            CloseDeviceFindHandle(found);
                        }
                    }
                }
            }

            foreach (var openRadio in btRadios) NativeImports.CloseHandle(openRadio);
            WiitarDebug.Log("FUNC END - RemoveAllWiimotes");
        }

        public void Sync()
        {
            WiitarDebug.Log("FUNC BEGIN - Sync");
            var radioParams  = new NativeImports.BLUETOOTH_FIND_RADIO_PARAMS();
            Guid HidServiceClass = Guid.Parse(NativeImports.HID_GUID);
            var btRadios     = new List<IntPtr>();
            IntPtr foundRadio;
            radioParams.Initialize();

            try
            {
                IntPtr foundResult = NativeImports.BluetoothFindFirstRadio(ref radioParams, out foundRadio);
                if (foundResult != IntPtr.Zero)
                {
                    bool more;
                    do
                    {
                        if (foundRadio != IntPtr.Zero) btRadios.Add(foundRadio);
                        more = NativeImports.BluetoothFindNextRadio(foundResult, out foundRadio);
                    } while (more);
                    NativeImports.BluetoothFindRadioClose(foundResult);
                }

                if (btRadios.Count > 0)
                {
                    Prompt("Searching for controllers...", isBold: true);
                    while (!Cancelled && Count == 0)
                    {
                        foreach (var radio in btRadios)
                        {
                            var radioInfo   = new NativeImports.BLUETOOTH_RADIO_INFO();
                            var deviceInfo  = new NativeImports.BLUETOOTH_DEVICE_INFO();
                            var searchParams= new NativeImports.BLUETOOTH_DEVICE_SEARCH_PARAMS();
                            radioInfo.Initialize(); deviceInfo.Initialize(); searchParams.Initialize();

                            uint getInfoError = NativeImports.BluetoothGetRadioInfo(radio, ref radioInfo);
                            if (getInfoError != 0)
                            {
                                Prompt("Found Bluetooth adapter but was unable to interact with it.");
                                if (Cancelled || Count > 0) break;
                                continue;
                            }

                            PrepareRadioForPairing(radio);
                            searchParams.hRadio = radio;
                            searchParams.fIssueInquiry = true;
                            searchParams.fReturnUnknown = true;
                            searchParams.fReturnConnected = true;
                            searchParams.fReturnRemembered = true;
                            searchParams.fReturnAuthenticated = true;
                            searchParams.cTimeoutMultiplier = ActiveInquiryTimeoutMultiplier;

                            IntPtr found = NativeImports.BluetoothFindFirstDevice(ref searchParams, ref deviceInfo);
                            try
                            {
                                if (found != IntPtr.Zero)
                                {
                                    do
                                    {
                                    if (!deviceInfo.szName.StartsWith("Nintendo RVL-CNT-01", StringComparison.Ordinal))
                                        continue;

                                    ProcessDiscoveredWiiDevice(radio, radioInfo, ref deviceInfo, ref HidServiceClass);
                                } while (!Cancelled && NativeImports.BluetoothFindNextDevice(found, ref deviceInfo));
                            }
                            }
                            finally
                            {
                                CloseDeviceFindHandle(found);
                            }

                            if (Cancelled || Count > 0) break;
                        }
                    }
                }
                else
                {
                    Prompt("No compatible Bluetooth Radios found (IF YOU SEE THIS MESSAGE, MENTION IT WHEN ASKING FOR HELP!).",
                        isBold: true, isItalic: true);
                    _notCompatable = true;
                }
            }
            finally
            {
                foreach (var openRadio in btRadios) NativeImports.CloseHandle(openRadio);
                _scanRunning = false;

                DispatcherQueue.TryEnqueue(() =>
                {
                    if (Count > 0)
                    {
                        Prompt("Device connected successfully. Give Windows up to a few minutes to install the drivers and it will show up in the list on the left.");
                        CloseButtonText = "OK";
                    }
                    else if (_notCompatable)
                    {
                        CloseButtonText = "OK";
                    }
                });

                WiitarDebug.Log("FUNC END - Sync");
            }
        }

        private void Prompt(string text, bool isBold = false, bool isItalic = false, bool isSmall = false, bool isDebug = false)
        {
            WiitarDebug.Log("SYNC DIALOG OUTPUT: \n\n" + text + "\n\n");
            DispatcherQueue.TryEnqueue(() =>
            {
                promptBox.Text += text + "\n";
                promptBox.SelectionStart  = promptBox.Text.Length;
                promptBox.SelectionLength = 0;
            });
        }
    }
}
