using System;
using System.Collections.Generic;
using Shared.Windows;

namespace WiinUSoft.Services;

/// <summary>Native Bluetooth pairing operations used by the synchronization workflow.</summary>
public sealed class BluetoothPairingService
{
    public const int ActiveInquiryTimeoutMultiplier = 4;

    public void PrepareRadio(IntPtr radio)
    {
        NativeImports.BluetoothEnableDiscovery(radio, true);
        NativeImports.BluetoothEnableIncomingConnections(radio, true);
    }

    public void CloseDeviceFindHandle(IntPtr handle)
    {
        if (handle != IntPtr.Zero)
            NativeImports.BluetoothFindDeviceClose(handle);
    }

    public bool TryActivateHidService(
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

    public void RemoveDevice(ref ulong address) => NativeImports.BluetoothRemoveDevice(ref address);
    public uint AuthenticateDevice(IntPtr radio, ref NativeImports.BLUETOOTH_DEVICE_INFO deviceInfo, string pin)
        => NativeImports.BluetoothAuthenticateDevice(IntPtr.Zero, radio, ref deviceInfo, pin, 6);

    public void RemoveAllWiimotes()
    {
        WiitarDebug.Log("FUNC BEGIN - RemoveAllWiimotes");
        var radioParams = new NativeImports.BLUETOOTH_FIND_RADIO_PARAMS();
        var radios = new List<IntPtr>();
        radioParams.Initialize();
        IntPtr radioFind = IntPtr.Zero;
        try
        {
            radioFind = NativeImports.BluetoothFindFirstRadio(ref radioParams, out var radio);
            if (radioFind != IntPtr.Zero)
            {
                do
                {
                    if (radio != IntPtr.Zero) radios.Add(radio);
                } while (NativeImports.BluetoothFindNextRadio(radioFind, out radio));
            }

            foreach (var openRadio in radios)
            {
                var radioInfo = new NativeImports.BLUETOOTH_RADIO_INFO();
                var deviceInfo = new NativeImports.BLUETOOTH_DEVICE_INFO();
                var search = new NativeImports.BLUETOOTH_DEVICE_SEARCH_PARAMS();
                radioInfo.Initialize(); deviceInfo.Initialize(); search.Initialize();
                if (NativeImports.BluetoothGetRadioInfo(openRadio, ref radioInfo) != 0) continue;

                PrepareRadio(openRadio);
                search.hRadio = openRadio;
                search.fIssueInquiry = true; search.fReturnUnknown = true; search.fReturnConnected = true;
                search.fReturnRemembered = true; search.fReturnAuthenticated = true;
                search.cTimeoutMultiplier = ActiveInquiryTimeoutMultiplier;
                IntPtr deviceFind = NativeImports.BluetoothFindFirstDevice(ref search, ref deviceInfo);
                try
                {
                    if (deviceFind == IntPtr.Zero) continue;
                    do
                    {
                        if (deviceInfo.szName.StartsWith("Nintendo RVL-CNT-01", StringComparison.Ordinal) &&
                            (deviceInfo.fRemembered || deviceInfo.fConnected))
                        {
                            uint error = NativeImports.BluetoothRemoveDevice(ref deviceInfo.Address);
                            if (error != 0) WiitarDebug.Log($"BluetoothRemoveDevice failed: 0x{error:X8}");
                        }
                    } while (NativeImports.BluetoothFindNextDevice(deviceFind, ref deviceInfo));
                }
                finally { CloseDeviceFindHandle(deviceFind); }
            }
        }
        finally
        {
            if (radioFind != IntPtr.Zero) NativeImports.BluetoothFindRadioClose(radioFind);
            foreach (var openRadio in radios) NativeImports.CloseHandle(openRadio);
            WiitarDebug.Log("FUNC END - RemoveAllWiimotes");
        }
    }
}
