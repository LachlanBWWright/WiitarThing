using System;
using System.Threading;
using NintrollerLib;
using Shared;
using WiinUSoft.Holders;
using WiinUSoft.VirtualOutput;

namespace WiinUSoft.Services;

/// <summary>
/// Owns the live controller session loop. It contains no XAML dependencies;
/// callers receive state/error callbacks and decide how to render them.
/// </summary>
internal sealed class DeviceInputRuntime
{
    private readonly ControllerInputMapper _mapper = new();
    private readonly RumbleController _rumble = new();
    private Timer? _pollTimer;
    private long _pollGeneration;

    public Holder? Holder { get; set; }
    public Property Properties { get; set; } = null!;
    public bool SnapIrPointer { get; set; }
    public bool Identifying { get; set; }
    public IR PreviousIr { get; set; }
    public ControllerOutputState LastSourceOutput { get; private set; } = ControllerOutputState.Empty;

    public void Update(Nintroller controller, IVirtualControllerBackend? backend)
    {
        Holder?.Update();
        _rumble.Step(controller, Holder, backend, Properties, Identifying);
    }

    public void StepRumble(Nintroller controller, IVirtualControllerBackend? backend)
        => _rumble.Step(controller, Holder, backend, Properties, Identifying);

    public void StartPolling(Nintroller controller, int player, TimerCallback callback, int updateSpeed)
    {
        controller.SetPlayerLED(player);
        StopPolling();
        var generation = Interlocked.Increment(ref _pollGeneration);
        _pollTimer = new Timer(callback, generation, 1000, updateSpeed);
    }

    public void StopPolling()
    {
        Interlocked.Increment(ref _pollGeneration);
        _pollTimer?.Dispose();
        _pollTimer = null;
    }

    public bool IsCurrentPoll(object? state)
        => state is long generation && generation == Interlocked.Read(ref _pollGeneration);

    public void OnControllerTypeChanged(ControllerType controllerType)
        => Holder?.AddMapping(controllerType);

    public void ProcessState(
        NintrollerStateEventArgs args,
        VirtualOutputSession virtualOutput,
        Action? previewChanged,
        Action<string, string>? inlineOutputError,
        Action<VirtualControllerError>? outputError)
    {
        if (Holder is null && virtualOutput.Backend is null)
            return;

        if (args.controllerType == ControllerType.Guitar)
        {
            LastSourceOutput = _mapper.ApplyGuitar(Holder, (WiiGuitar)args.state);
            if (virtualOutput.Backend is not null)
            {
                var writeResult = virtualOutput.Backend.Update(LastSourceOutput);
                if (writeResult.IsError)
                {
                    if (!virtualOutput.BackendWriteFailed)
                    {
                        virtualOutput.BackendWriteFailed = true;
                        if (virtualOutput.ShouldKeepWriteErrorInline(writeResult.Error))
                            inlineOutputError?.Invoke(virtualOutput.Backend.DisplayName, writeResult.Error.Message);
                        else
                            outputError?.Invoke(writeResult.Error);
                    }
                }
                else
                {
                    if (virtualOutput.BackendWriteFailed)
                        inlineOutputError?.Invoke(virtualOutput.Backend.DisplayName, string.Empty);
                    virtualOutput.BackendWriteFailed = false;
                }
            }
            previewChanged?.Invoke();
            return;
        }

        if (Holder is null)
            return;

        Holder.ClearAllValues();
        PreviousIr = _mapper.ApplyNonGuitar(Holder, args.controllerType, args.state, SnapIrPointer, Properties, PreviousIr);
        Holder.Update();
    }
}
