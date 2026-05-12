using System;
using System.Collections.Generic;
using System.IO;
using NintrollerLib;
using Shared;
using System.Runtime.InteropServices;

namespace WiinUSoft.VirtualOutput;

internal sealed class VJoyBackend : IVirtualControllerBackend
{
    public string DisplayName => "DirectInput Joystick (vJoy, experimental)";
    public VirtualOutputMode Mode => VirtualOutputMode.VJoyExperimental;
    private bool _connected;
    private uint _deviceId;

    private const uint HID_USAGE_X = 0x30;
    private const uint HID_USAGE_Y = 0x31;
    internal const string VJOY_INTERFACE_DLL = "vJoyInterface.dll";

    static VJoyBackend()
    {
        VirtualOutputNativeLibraryResolver.EnsureRegistered();
    }

    private enum VjdStat
    {
        VjdStatOwn = 0,
        VjdStatFree = 1,
        VjdStatBust = 2,
        VjdStatMiss = 3,
        VjdStatUnkn = 4
    }

    public static bool IsDriverAvailable()
    {
        try
        {
            return vJoyEnabled();
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    public Result<Unit, VirtualControllerError> Connect(int slotOrDeviceId, ControllerType sourceType)
    {
        if (sourceType != ControllerType.Guitar)
        {
            return Result<Unit, VirtualControllerError>.Err(
                VirtualControllerError.InvalidMapping("vJoy backend currently supports guitar output only.", slotOrDeviceId > 0 ? slotOrDeviceId : null));
        }

        if (slotOrDeviceId <= 0)
        {
            return Result<Unit, VirtualControllerError>.Err(
                VirtualControllerError.InvalidMapping("vJoy device ID must be 1 or greater.", slotOrDeviceId));
        }

        _deviceId = (uint)slotOrDeviceId;

        bool enabled;
        try
        {
            enabled = vJoyEnabled();
        }
        catch (DllNotFoundException ex)
        {
            return Result<Unit, VirtualControllerError>.Err(
                VirtualControllerError.DriverNotReady("vJoyInterface.dll was not found. Install vJoy, then restart WiitarThing before selecting this backend.", slotOrDeviceId, ex));
        }
        catch (EntryPointNotFoundException ex)
        {
            return Result<Unit, VirtualControllerError>.Err(
                VirtualControllerError.DriverNotReady("vJoy API entry points are unavailable. Reinstall vJoy.", slotOrDeviceId, ex));
        }

        if (!enabled)
        {
            return Result<Unit, VirtualControllerError>.Err(
                VirtualControllerError.DriverNotReady("vJoy is installed but not enabled.", slotOrDeviceId));
        }

        try
        {
            var status = GetVJDStatus(_deviceId);
            if (status == VjdStat.VjdStatMiss)
            {
                return Result<Unit, VirtualControllerError>.Err(
                    VirtualControllerError.DriverNotReady($"vJoy device {_deviceId} does not exist. Configure it in vJoyConf.", slotOrDeviceId));
            }
            if (status == VjdStat.VjdStatBust)
            {
                return Result<Unit, VirtualControllerError>.Err(
                    VirtualControllerError.ConnectionFailed($"vJoy device {_deviceId} is busy.", slotOrDeviceId));
            }

            int buttonCount = GetVJDButtonNumber(_deviceId);
            int povCount = GetVJDDiscPovNumber(_deviceId);
            bool hasXAxis = GetVJDAxisExist(_deviceId, HID_USAGE_X);
            bool hasYAxis = GetVJDAxisExist(_deviceId, HID_USAGE_Y);
            if (buttonCount < 8 || povCount < 1 || !hasXAxis || !hasYAxis)
            {
                return Result<Unit, VirtualControllerError>.Err(
                    VirtualControllerError.InvalidMapping(
                        $"vJoy device {_deviceId} is underconfigured. Required: >=8 buttons, >=1 POV, X/Y axes.",
                        slotOrDeviceId));
            }

            if (!AcquireVJD(_deviceId))
            {
                return Result<Unit, VirtualControllerError>.Err(
                    VirtualControllerError.ConnectionFailed($"Failed to acquire vJoy device {_deviceId}.", slotOrDeviceId));
            }

            _connected = true;
            ResetVJD(_deviceId);
            return Result<Unit, VirtualControllerError>.Ok(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, VirtualControllerError>.Err(
                VirtualControllerError.ConnectionFailed($"vJoy connection failed: {ex.Message}", slotOrDeviceId, ex));
        }
    }

    public Result<Unit, VirtualControllerError> Update(ControllerOutputState state)
    {
        if (!_connected || _deviceId == 0)
        {
            return Result<Unit, VirtualControllerError>.Err(
                VirtualControllerError.ConnectionFailed("vJoy virtual output is not connected.", _deviceId > 0 ? (int)_deviceId : null));
        }

        try
        {
            var report = new JOYSTICK_POSITION_V2
            {
                bDevice = (byte)_deviceId,
                wAxisX = ToVJoyAxis(state.Whammy),
                wAxisY = ToVJoyAxis(state.Tilt),
                bHats = unchecked((uint)ToPov(state))
            };

            SetButton(ref report, 1, state.Green);
            SetButton(ref report, 2, state.Red);
            SetButton(ref report, 3, state.Yellow);
            SetButton(ref report, 4, state.Blue);
            SetButton(ref report, 5, state.Orange);
            SetButton(ref report, 6, state.Start);
            SetButton(ref report, 7, state.Select);
            SetButton(ref report, 8, state.Home);

            if (!UpdateVJD(_deviceId, ref report))
            {
                return Result<Unit, VirtualControllerError>.Err(
                    VirtualControllerError.WriteFailed($"Failed to send state to vJoy device {_deviceId}.", (int)_deviceId));
            }

            return Result<Unit, VirtualControllerError>.Ok(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, VirtualControllerError>.Err(
                VirtualControllerError.WriteFailed($"vJoy update failed: {ex.Message}", (int)_deviceId, ex));
        }
    }

    public Result<Unit, VirtualControllerError> Disconnect()
    {
        if (_connected && _deviceId > 0)
        {
            try
            {
                ResetVJD(_deviceId);
                RelinquishVJD(_deviceId);
            }
            catch { }
        }

        _connected = false;
        _deviceId = 0;
        return Result<Unit, VirtualControllerError>.Ok(Unit.Value);
    }

    public Result<VirtualControllerIdentity, VirtualControllerError> GetIdentity()
    {
        if (!_connected || _deviceId == 0)
        {
            return Result<VirtualControllerIdentity, VirtualControllerError>.Err(
                VirtualControllerError.ConnectionFailed("vJoy virtual output is not connected."));
        }

        return Result<VirtualControllerIdentity, VirtualControllerError>.Ok(
            new VirtualControllerIdentity(DisplayName, ProductName: $"vJoy Device {_deviceId}", InstanceId: $"vJoy:{_deviceId}"));
    }

    public Result<IVirtualControllerReadback, VirtualControllerError> CreateReadback()
    {
        if (!_connected || _deviceId == 0)
        {
            return Result<IVirtualControllerReadback, VirtualControllerError>.Err(
                VirtualControllerError.ConnectionFailed("vJoy virtual output is not connected."));
        }

        return Result<IVirtualControllerReadback, VirtualControllerError>.Ok(new VJoyReadback(_deviceId));
    }

    public void Dispose()
    {
        Disconnect();
    }

    private static int ToVJoyAxis(float value)
    {
        float clamped = ControllerOutputState.ClampSigned(value);
        return (int)Math.Round((clamped + 1f) * 16384f);
    }

    private static int ToPov(ControllerOutputState state)
    {
        if (state.StrumUp && state.DPadRight) return 4500;
        if (state.StrumUp && state.DPadLeft) return 31500;
        if (state.StrumDown && state.DPadRight) return 13500;
        if (state.StrumDown && state.DPadLeft) return 22500;
        if (state.StrumUp) return 0;
        if (state.StrumDown) return 18000;
        if (state.DPadRight) return 9000;
        if (state.DPadLeft) return 27000;
        return -1;
    }

    private static void SetButton(ref JOYSTICK_POSITION_V2 report, int index, bool pressed)
    {
        if (!pressed || index < 1 || index > 32)
            return;

        report.lButtons |= 1u << (index - 1);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOYSTICK_POSITION_V2
    {
        public byte bDevice;
        public int wThrottle;
        public int wRudder;
        public int wAileron;
        public int wAxisX;
        public int wAxisY;
        public int wAxisZ;
        public int wAxisXRot;
        public int wAxisYRot;
        public int wAxisZRot;
        public int wSlider;
        public int wDial;
        public int wWheel;
        public int wAxisVX;
        public int wAxisVY;
        public int wAxisVZ;
        public int wAxisVBRX;
        public int wAxisVBRY;
        public int wAxisVBRZ;
        public uint lButtons;
        public uint bHats;
        public uint bHatsEx1;
        public uint bHatsEx2;
        public uint bHatsEx3;
    }

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool vJoyEnabled();

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern VjdStat GetVJDStatus(uint rID);

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool AcquireVJD(uint rID);

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void RelinquishVJD(uint rID);

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool ResetVJD(uint rID);

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool UpdateVJD(uint rID, ref JOYSTICK_POSITION_V2 pData);

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int GetVJDButtonNumber(uint rID);

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int GetVJDDiscPovNumber(uint rID);

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool GetVJDAxisExist(uint rID, uint Axis);

    internal static string? FindVJoyInterfacePath()
    {
        foreach (string path in GetVJoyInterfaceSearchPaths())
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static IEnumerable<string> GetVJoyInterfaceSearchPaths()
    {
        string archFolder = Environment.Is64BitProcess ? "x64" : "x86";
        foreach (string root in GetApplicationSearchRoots())
        {
            yield return Path.Combine(root, VJOY_INTERFACE_DLL);
            yield return Path.Combine(root, "Drivers", "vJoy", archFolder, VJOY_INTERFACE_DLL);
            yield return Path.Combine(root, "Drivers", "vJoy", VJOY_INTERFACE_DLL);
            yield return Path.Combine(root, "vJoy", archFolder, VJOY_INTERFACE_DLL);
            yield return Path.Combine(root, "vJoy", VJOY_INTERFACE_DLL);
        }

        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            yield return Path.Combine(programFiles, "vJoy", archFolder, VJOY_INTERFACE_DLL);
            yield return Path.Combine(programFiles, "vJoy", VJOY_INTERFACE_DLL);
        }

        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            yield return Path.Combine(programFilesX86, "vJoy", archFolder, VJOY_INTERFACE_DLL);
            yield return Path.Combine(programFilesX86, "vJoy", VJOY_INTERFACE_DLL);
        }
    }

    private static IEnumerable<string> GetApplicationSearchRoots()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; current != null && i < 8; i++)
        {
            yield return current.FullName;
            current = current.Parent;
        }
    }

    private sealed class VJoyReadback : IVirtualControllerReadback
    {
        private readonly uint _deviceId;
        public string DisplayName => $"vJoy Device {_deviceId} Readback";

        public VJoyReadback(uint deviceId)
        {
            _deviceId = deviceId;
        }

        public Result<Unit, VirtualControllerError> Attach(VirtualControllerIdentity identity)
        {
            return Result<Unit, VirtualControllerError>.Ok(Unit.Value);
        }

        public Result<ControllerOutputState, VirtualControllerError> ReadState()
        {
            return Result<ControllerOutputState, VirtualControllerError>.Err(
                VirtualControllerError.DriverNotReady("vJoy readback is not available. Use Windows joy.cpl or a DirectInput test tool to verify output."));
        }

        public void Dispose()
        {
        }
    }
}

