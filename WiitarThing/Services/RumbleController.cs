using System;
using NintrollerLib;
using WiinUSoft.Holders;
using WiinUSoft.VirtualOutput;

namespace WiinUSoft.Services;

/// <summary>Applies controller rumble duty-cycle policy without view dependencies.</summary>
internal sealed class RumbleController
{
    private const int RumbleStepPeriod = 10;
    private const float RumbleSlowMultiplier = 0.5f;
    private int _stepCount;

    public void Step(Nintroller controller, Holder? holder, IVirtualControllerBackend? backend, Property properties, bool identifying)
    {
        if ((holder is null && backend is null) || identifying)
            return;

        bool current = controller.RumbleEnabled;
        if (!properties.useRumble)
        {
            if (current)
                controller.RumbleEnabled = false;
            return;
        }

        float rumbleAmount = holder?.RumbleAmount
            ?? (backend as ScpXInputBackend)?.LastRumbleAmount
            ?? 0f;
        float modifier = properties.rumbleIntensity * 0.5f;
        float dutyCycle = rumbleAmount < 256
            ? RumbleSlowMultiplier * rumbleAmount / 256f
            : rumbleAmount / 65535f;
        int stopStep = (int)Math.Round(modifier * dutyCycle * RumbleStepPeriod);
        if (_stepCount < stopStep)
        {
            if (!current)
                controller.RumbleEnabled = true;
        }
        else if (current)
        {
            controller.RumbleEnabled = false;
        }

        if (++_stepCount >= RumbleStepPeriod)
            _stepCount = 0;
    }
}
