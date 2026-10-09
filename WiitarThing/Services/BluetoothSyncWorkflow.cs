using System;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Threading;
using Shared.Windows;

namespace WiinUSoft.Services;

/// <summary>Runs the Bluetooth inquiry and pairing workflow without any UI dependencies.</summary>
public sealed class BluetoothSyncWorkflow
{
    private const byte InquiryTimeoutMultiplier = 4;
    private readonly BluetoothPairingService _pairing;

    public BluetoothSyncWorkflow(BluetoothPairingService? pairing = null) => _pairing = pairing ?? new BluetoothPairingService();

    public void Run(CancellationToken cancellationToken, Action<string> report, Action deviceFound)
    {
        var radios = FindRadios();
        try
        {
            if (radios.Count == 0)
            {
                report("No compatible Bluetooth Radios found.");
                return;
            }

            report("Searching for controllers...");
            Guid hidClass = Guid.Parse(NativeImports.HID_GUID);
            while (!cancellationToken.IsCancellationRequested)
            {
                foreach (var radio in radios)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    ScanRadio(radio, ref hidClass, cancellationToken, report, deviceFound);
                }
                break;
            }
        }
        finally
        {
            foreach (var radio in radios) NativeImports.CloseHandle(radio);
        }
    }

    private void ScanRadio(IntPtr radio, ref Guid hidClass, CancellationToken token, Action<string> report, Action found)
    {
        var info = new NativeImports.BLUETOOTH_RADIO_INFO();
        info.Initialize();
        if (NativeImports.BluetoothGetRadioInfo(radio, ref info) != 0)
        {
            report("Found Bluetooth adapter but was unable to interact with it.");
            return;
        }

        _pairing.PrepareRadio(radio);
        var search = new NativeImports.BLUETOOTH_DEVICE_SEARCH_PARAMS();
        var device = new NativeImports.BLUETOOTH_DEVICE_INFO();
        search.Initialize(); device.Initialize();
        search.hRadio = radio; search.fIssueInquiry = true; search.fReturnUnknown = true;
        search.fReturnConnected = true; search.fReturnRemembered = true; search.fReturnAuthenticated = true;
        search.cTimeoutMultiplier = InquiryTimeoutMultiplier;
        IntPtr handle = NativeImports.BluetoothFindFirstDevice(ref search, ref device);
        try
        {
            if (handle == IntPtr.Zero) return;
            do
            {
                if (token.IsCancellationRequested) return;
                if (device.szName.StartsWith("Nintendo RVL-CNT-01", StringComparison.Ordinal))
                    PairDevice(radio, ref device, ref hidClass, report, found);
            } while (NativeImports.BluetoothFindNextDevice(handle, ref device));
        }
        finally { _pairing.CloseDeviceFindHandle(handle); }
    }

    private void PairDevice(IntPtr radio, ref NativeImports.BLUETOOTH_DEVICE_INFO device, ref Guid hidClass, Action<string> report, Action found)
    {
        report($"Found controller (\"{device.szName}\"). Attempting to pair...");
        if (device.fRemembered && device.fConnected && _pairing.TryActivateHidService(radio, ref device, ref hidClass, out _, out _))
        {
            report("Controller service refreshed. Waiting for Windows to expose it as a HID device...");
            found();
            return;
        }

        var pin = BuildPin(device.Address);
        uint auth = _pairing.AuthenticateDevice(radio, ref device, pin);
        if (auth != 0)
        {
            report("Bluetooth authentication failed: " + GetAuthenticationError(auth));
            return;
        }

        if (!_pairing.TryActivateHidService(radio, ref device, ref hidClass, out uint service, out uint activate))
        {
            report($"Bluetooth HID activation failed (service: 0x{service:X8}, activation: 0x{activate:X8}).");
            return;
        }
        report("Successfully Paired!");
        found();
    }

    private static string BuildPin(ulong address)
    {
        var bytes = BitConverter.GetBytes(address);
        var pin = new StringBuilder();
        for (int i = 0; i < 6; i++) if (bytes[i] > 0) pin.Append((char)bytes[i]);
        return pin.ToString();
    }

    private static string GetAuthenticationError(uint code)
        => $"(ERROR CODE 0x{code.ToString("X", CultureInfo.InvariantCulture)})";

    private static System.Collections.Generic.List<IntPtr> FindRadios()
    {
        var result = new System.Collections.Generic.List<IntPtr>();
        var parameters = new NativeImports.BLUETOOTH_FIND_RADIO_PARAMS();
        parameters.Initialize();
        IntPtr handle = NativeImports.BluetoothFindFirstRadio(ref parameters, out var radio);
        try
        {
            if (handle == IntPtr.Zero) return result;
            do { if (radio != IntPtr.Zero) result.Add(radio); }
            while (NativeImports.BluetoothFindNextRadio(handle, out radio));
            return result;
        }
        finally { if (handle != IntPtr.Zero) NativeImports.BluetoothFindRadioClose(handle); }
    }
}
