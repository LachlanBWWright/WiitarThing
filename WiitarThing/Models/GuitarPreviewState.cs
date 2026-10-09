namespace WiinUSoft.Models;

public readonly record struct GuitarPreviewState(
    bool Green, bool Red, bool Yellow, bool Blue, bool Orange,
    bool StrumUp, bool StrumDown, bool DPadLeft, bool DPadRight,
    bool Start, bool Select, float Whammy, float Tilt)
{
    public static GuitarPreviewState Empty => default;

    public string ToCompactDebugString()
        => $"G:{(Green ? 1 : 0)} R:{(Red ? 1 : 0)} Y:{(Yellow ? 1 : 0)} B:{(Blue ? 1 : 0)} O:{(Orange ? 1 : 0)} " +
           $"SU:{(StrumUp ? 1 : 0)} SD:{(StrumDown ? 1 : 0)} Wh:{Whammy:0.00} Tl:{Tilt:0.00}";
}
