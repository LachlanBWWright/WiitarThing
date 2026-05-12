using System;
using System.Runtime.InteropServices;

namespace WiinUSoft.VirtualOutput;

internal static class XInputNative
{
    private const ushort XINPUT_GAMEPAD_DPAD_UP = 0x0001;
    private const ushort XINPUT_GAMEPAD_DPAD_DOWN = 0x0002;
    private const ushort XINPUT_GAMEPAD_DPAD_LEFT = 0x0004;
    private const ushort XINPUT_GAMEPAD_DPAD_RIGHT = 0x0008;
    private const ushort XINPUT_GAMEPAD_START = 0x0010;
    private const ushort XINPUT_GAMEPAD_BACK = 0x0020;
    private const ushort XINPUT_GAMEPAD_LEFT_THUMB = 0x0040;
    private const ushort XINPUT_GAMEPAD_RIGHT_THUMB = 0x0080;
    private const ushort XINPUT_GAMEPAD_LEFT_SHOULDER = 0x0100;
    private const ushort XINPUT_GAMEPAD_RIGHT_SHOULDER = 0x0200;
    private const ushort XINPUT_GAMEPAD_GUIDE = 0x0400;
    private const ushort XINPUT_GAMEPAD_A = 0x1000;
    private const ushort XINPUT_GAMEPAD_B = 0x2000;
    private const ushort XINPUT_GAMEPAD_X = 0x4000;
    private const ushort XINPUT_GAMEPAD_Y = 0x8000;

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState", CallingConvention = CallingConvention.StdCall)]
    private static extern uint XInputGetState14(uint dwUserIndex, out XInputState pState);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState", CallingConvention = CallingConvention.StdCall)]
    private static extern uint XInputGetState910(uint dwUserIndex, out XInputState pState);

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetCapabilities", CallingConvention = CallingConvention.StdCall)]
    private static extern uint XInputGetCapabilities14(uint dwUserIndex, uint dwFlags, out XInputCapabilities pCapabilities);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetCapabilities", CallingConvention = CallingConvention.StdCall)]
    private static extern uint XInputGetCapabilities910(uint dwUserIndex, uint dwFlags, out XInputCapabilities pCapabilities);

    public static bool TryGetState(int slot, out ControllerOutputState state, bool useXbox360GuitarFrets = false)
    {
        state = ControllerOutputState.Empty;
        if (slot < 1 || slot > 4)
            return false;

        var userIndex = (uint)(slot - 1);
        uint result;
        XInputState rawState;
        try
        {
            result = XInputGetState14(userIndex, out rawState);
        }
        catch (DllNotFoundException)
        {
            result = XInputGetState910(userIndex, out rawState);
        }
        catch (EntryPointNotFoundException)
        {
            result = XInputGetState910(userIndex, out rawState);
        }

        if (result != 0)
            return false;

        ushort buttons = rawState.Gamepad.wButtons;
        bool yellow = useXbox360GuitarFrets
            ? (buttons & XINPUT_GAMEPAD_X) != 0
            : (buttons & XINPUT_GAMEPAD_Y) != 0;
        bool blue = useXbox360GuitarFrets
            ? (buttons & XINPUT_GAMEPAD_Y) != 0
            : (buttons & XINPUT_GAMEPAD_X) != 0;
        state = new ControllerOutputState
        {
            Green = (buttons & XINPUT_GAMEPAD_A) != 0,
            Red = (buttons & XINPUT_GAMEPAD_B) != 0,
            Yellow = yellow,
            Blue = blue,
            Orange = (buttons & XINPUT_GAMEPAD_LEFT_SHOULDER) != 0,
            StrumUp = (buttons & XINPUT_GAMEPAD_DPAD_UP) != 0,
            StrumDown = (buttons & XINPUT_GAMEPAD_DPAD_DOWN) != 0,
            DPadLeft = (buttons & XINPUT_GAMEPAD_DPAD_LEFT) != 0,
            DPadRight = (buttons & XINPUT_GAMEPAD_DPAD_RIGHT) != 0,
            Start = (buttons & XINPUT_GAMEPAD_START) != 0,
            Select = (buttons & XINPUT_GAMEPAD_BACK) != 0,
            Home = (buttons & XINPUT_GAMEPAD_GUIDE) != 0,
            Whammy = ControllerOutputState.ClampSigned(rawState.Gamepad.sThumbRX / 32767f),
            Tilt = ControllerOutputState.ClampSigned(rawState.Gamepad.sThumbRY / 32767f)
        };

        return true;
    }

    public static bool TryGetSubType(int slot, out byte subType)
    {
        subType = 0;
        if (slot < 1 || slot > 4)
            return false;

        uint result;
        XInputCapabilities capabilities;
        var userIndex = (uint)(slot - 1);
        try
        {
            result = XInputGetCapabilities14(userIndex, 0, out capabilities);
        }
        catch (DllNotFoundException)
        {
            result = XInputGetCapabilities910(userIndex, 0, out capabilities);
        }
        catch (EntryPointNotFoundException)
        {
            result = XInputGetCapabilities910(userIndex, 0, out capabilities);
        }

        if (result != 0)
            return false;

        subType = capabilities.SubType;
        return true;
    }

    public static bool IsGuitarSubType(byte subType)
    {
        return subType == 0x06 || subType == 0x07 || subType == 0x0B;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint dwPacketNumber;
        public XInputGamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputCapabilities
    {
        public byte Type;
        public byte SubType;
        public ushort Flags;
        public XInputGamepad Gamepad;
        public XInputVibration Vibration;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputVibration
    {
        public ushort wLeftMotorSpeed;
        public ushort wRightMotorSpeed;
    }
}
