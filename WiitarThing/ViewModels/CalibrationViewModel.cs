using NintrollerLib;
using System.Collections.Generic;

namespace WiinUSoft.ViewModels;

public sealed class CalibrationViewModel : ViewModelBase
{
    private bool _doSave;
    private ControllerType _calibrationTarget = ControllerType.Unknown;
    private string _title = "Calibration";
    private string _instructions = string.Empty;
    private string _nextButtonText = "Next";

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

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Instructions
    {
        get => _instructions;
        set => SetProperty(ref _instructions, value);
    }

    public string NextButtonText
    {
        get => _nextButtonText;
        set => SetProperty(ref _nextButtonText, value);
    }

    public HashSet<ControllerType> CalibratedTypes { get; } = new();
}
