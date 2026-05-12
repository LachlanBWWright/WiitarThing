using NintrollerLib;
using System.Collections.Generic;

namespace WiinUSoft.ViewModels;

public sealed class CalibrationViewModel : ViewModelBase
{
    private bool _doSave;
    private ControllerType _calibrationTarget = ControllerType.Unknown;

    public bool DoSave
    {
        get => _doSave;
        set => SetProperty(ref _doSave, value);
    }

    public ControllerType CalibrationTarget
    {
        get => _calibrationTarget;
        set => SetProperty(ref _calibrationTarget, value);
    }

    public HashSet<ControllerType> CalibratedTypes { get; } = new();
}
