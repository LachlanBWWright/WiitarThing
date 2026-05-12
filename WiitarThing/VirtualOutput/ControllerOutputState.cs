using NintrollerLib;

namespace WiinUSoft.VirtualOutput;

internal sealed record ControllerOutputState
{
    public bool Green { get; init; }
    public bool Red { get; init; }
    public bool Yellow { get; init; }
    public bool Blue { get; init; }
    public bool Orange { get; init; }

    public bool StrumUp { get; init; }
    public bool StrumDown { get; init; }
    public bool DPadLeft { get; init; }
    public bool DPadRight { get; init; }

    public bool Start { get; init; }
    public bool Select { get; init; }
    public bool Home { get; init; }

    public float Whammy { get; init; }
    public float Tilt { get; init; }

    public static ControllerOutputState Empty { get; } = new();

    public static ControllerOutputState FromWiiGuitar(WiiGuitar guitar)
    {
        return new ControllerOutputState
        {
            Green = guitar.G,
            Red = guitar.R,
            Yellow = guitar.Y,
            Blue = guitar.B,
            Orange = guitar.O,
            StrumUp = guitar.Up,
            StrumDown = guitar.Down,
            DPadLeft = guitar.Left,
            DPadRight = guitar.Right,
            Start = guitar.Start,
            Select = guitar.Select,
            Home = guitar.wiimote.buttons.Home,
            Whammy = ClampSigned(guitar.WhammyHigh + guitar.WhammyLow),
            Tilt = ClampSigned(guitar.TiltHigh + guitar.TiltLow)
        };
    }

    public static float ClampSigned(float value) => value < -1f ? -1f : value > 1f ? 1f : value;

    public string ToCompactDebugString()
    {
        static string B(bool value) => value ? "1" : "0";
        return $"G{B(Green)} R{B(Red)} Y{B(Yellow)} B{B(Blue)} O{B(Orange)} SU{B(StrumUp)} SD{B(StrumDown)} DL{B(DPadLeft)} DR{B(DPadRight)} St{B(Start)} Sl{B(Select)} Hm{B(Home)} Wh{Whammy:0.00} Tl{Tilt:0.00}";
    }
}
