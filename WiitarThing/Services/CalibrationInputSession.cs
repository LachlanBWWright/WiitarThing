using System;
using NintrollerLib;

namespace WiinUSoft.Services;

/// <summary>Owns controller event subscriptions while calibration is open.</summary>
internal sealed class CalibrationInputSession : ICalibrationInputSession
{
    private readonly Nintroller _controller;

    public CalibrationInputSession(Nintroller controller)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _controller.StateUpdate += ControllerStateUpdate;
        _controller.ExtensionChange += ControllerExtensionChange;
    }

    public ControllerType DeviceType => _controller.Type;
    public string StoredCalibrations => _controller.StoredCalibrations.ToString();

    public event EventHandler<NintrollerStateEventArgs>? StateUpdated;
    public event EventHandler<NintrollerExtensionEventArgs>? ExtensionChanged;

    private void ControllerStateUpdate(object? sender, NintrollerStateEventArgs e) => StateUpdated?.Invoke(this, e);
    private void ControllerExtensionChange(object? sender, NintrollerExtensionEventArgs e) => ExtensionChanged?.Invoke(this, e);

    public void Dispose()
    {
        _controller.StateUpdate -= ControllerStateUpdate;
        _controller.ExtensionChange -= ControllerExtensionChange;
    }
}

public interface ICalibrationInputSession : IDisposable
{
    ControllerType DeviceType { get; }
    string StoredCalibrations { get; }
    event EventHandler<NintrollerStateEventArgs>? StateUpdated;
    event EventHandler<NintrollerExtensionEventArgs>? ExtensionChanged;
}
