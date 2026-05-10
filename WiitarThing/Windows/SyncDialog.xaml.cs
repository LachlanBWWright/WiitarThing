using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Shared.Windows;

namespace WiinUSoft.Windows
{
    public partial class SyncDialog : ContentDialog
    {
        public bool Cancelled { get; protected set; }
        public int Count { get; protected set; }

        private bool _notCompatable;
        private bool _startFired;
        private bool _scanRunning;
        private Task? _scanTask;

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
                _                           => "(ERROR CODE 0x" + errCode.ToString("X") + ")"
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

        public static void RemoveAllWiimotes()
        {
            WiitarDebug.Log("FUNC BEGIN - RemoveAllWiimotes");
            var radioParams = new NativeImports.BLUETOOTH_FIND_RADIO_PARAMS();
            var btRadios  = new List<IntPtr>();
            IntPtr foundRadio;
            radioParams.Initialize();

            IntPtr foundResult = NativeImports.BluetoothFindFirstRadio(ref radioParams, out foundRadio);
            bool more = foundResult != IntPtr.Zero;
            do
            {
                if (foundRadio != IntPtr.Zero) btRadios.Add(foundRadio);
                more = NativeImports.BluetoothFindNextRadio(ref radioParams, out foundRadio);
            } while (more);

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
                        searchParams.hRadio = radio;
                        searchParams.fIssueInquiry = true;
                        searchParams.fReturnUnknown = true;
                        searchParams.fReturnConnected = true;
                        searchParams.fReturnRemembered = true;
                        searchParams.fReturnAuthenticated = true;
                        searchParams.cTimeoutMultiplier = 2;

                        IntPtr found = NativeImports.BluetoothFindFirstDevice(ref searchParams, ref deviceInfo);
                        if (found != IntPtr.Zero)
                        {
                            do
                            {
                                if (deviceInfo.szName.StartsWith("Nintendo RVL-CNT-01") &&
                                    (deviceInfo.fRemembered || deviceInfo.fConnected))
                                {
                                    NativeImports.BluetoothRemoveDevice(ref deviceInfo.Address);
                                }
                            } while (NativeImports.BluetoothFindNextDevice(found, ref deviceInfo));
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
                bool more = foundResult != IntPtr.Zero;
                do
                {
                    if (foundRadio != IntPtr.Zero) btRadios.Add(foundRadio);
                    more = NativeImports.BluetoothFindNextRadio(ref radioParams, out foundRadio);
                } while (more);

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
                            if (getInfoError == 0)
                            {
                                searchParams.hRadio = radio;
                                searchParams.fIssueInquiry = true;
                                searchParams.fReturnUnknown = true;
                                searchParams.fReturnConnected = false;
                                searchParams.fReturnRemembered = true;
                                searchParams.fReturnAuthenticated = false;
                                searchParams.cTimeoutMultiplier = 2;

                                IntPtr found = NativeImports.BluetoothFindFirstDevice(ref searchParams, ref deviceInfo);
                                if (found != IntPtr.Zero)
                                {
                                    do
                                    {
                                        if (deviceInfo.szName.StartsWith("Nintendo RVL-CNT-01"))
                                        {
                                            var str_fRemembered = deviceInfo.fRemembered ? ", but it is already synced!" : ". Attempting to pair now...";
                                            string label = deviceInfo.szName switch
                                            {
                                                "Nintendo RVL-CNT-01"    => "Found Wiimote",
                                                "Nintendo RVL-CNT-01-TR" => "Found 2nd-Gen Wiimote+",
                                                "Nintendo RVL-CNT-01-UC" => "Found Wii U Pro Controller",
                                                _                        => "Found Unknown Wii Device Type"
                                            };
                                            Prompt($"{label} (\"{deviceInfo.szName}\"){str_fRemembered}",
                                                isBold: !deviceInfo.fRemembered, isItalic: deviceInfo.fRemembered);

                                            if (deviceInfo.fRemembered) continue;

                                            var password = new StringBuilder();
                                            uint pcService = 16;
                                            Guid[] guids = new Guid[16];
                                            bool success = true;
                                            var bytes = BitConverter.GetBytes(radioInfo.address);
                                            for (int i = 0; i < 6; i++) if (bytes[i] > 0) password.Append((char)bytes[i]);

                                            uint errForget = 0, errAuth = 0, errService = 0, errActivate = 0;

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
                                                errService = NativeImports.BluetoothEnumerateInstalledServices(radio, ref deviceInfo, ref pcService, guids);
                                                success = errService == 0;
                                            }

                                            if (success)
                                            {
                                                errActivate = NativeImports.BluetoothSetServiceState(radio, ref deviceInfo, ref HidServiceClass, 0x01);
                                                success = errActivate == 0;
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
                                    } while (!Cancelled && NativeImports.BluetoothFindNextDevice(found, ref deviceInfo));
                                }
                            }
                            else
                            {
                                Prompt("Found Bluetooth adapter but was unable to interact with it.");
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
