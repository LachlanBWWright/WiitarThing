using System;
using System.Collections.Generic;
using NintrollerLib;
using Shared;
using WiinUSoft.VirtualOutput;

namespace WiinUSoft.Services;

/// <summary>Owns virtual-controller backend and readback lifetime independently of the view.</summary>
internal sealed class VirtualOutputSession : IDisposable
{
    private static readonly object VJoyGate = new();
    private static readonly HashSet<int> VJoyReservations = new();
    private int? _reservedVJoyId;
    private readonly IDeviceConfigurationService _configuration;
    private readonly Func<Property> _deviceProperties;
    private readonly Func<VirtualOutputMode, IVirtualControllerBackend> _backendFactory;

    public VirtualOutputSession(IDeviceConfigurationService configuration, Func<Property> deviceProperties,
        Func<VirtualOutputMode, IVirtualControllerBackend>? backendFactory = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _deviceProperties = deviceProperties ?? throw new ArgumentNullException(nameof(deviceProperties));
        _backendFactory = backendFactory ?? VirtualControllerBackendFactory.Create;
    }

    public IVirtualControllerBackend? Backend { get; private set; }
    public IVirtualControllerReadback? Readback { get; private set; }
    public VirtualControllerIdentity? Identity { get; private set; }
    public ControllerOutputState LastReadbackOutput { get; set; } = ControllerOutputState.Empty;
    public bool BackendWriteFailed { get; set; }

    public Result<Unit, VirtualControllerError> Connect(int xInputSlot, ControllerType sourceType)
    {
        Disconnect();
        BackendWriteFailed = false;
        LastReadbackOutput = ControllerOutputState.Empty;

        var properties = _deviceProperties();
        var mode = properties.virtualOutputMode ?? _configuration.VirtualOutputMode;
        Backend = _backendFactory(mode);
        if (mode == VirtualOutputMode.HidMaestroExperimental
            && (properties.guitarPreviewMode ?? _configuration.GuitarPreviewMode) != GuitarPreviewMode.VirtualOutput)
        {
            properties.guitarPreviewMode = GuitarPreviewMode.VirtualOutput;
            _configuration.AddDevicePreference(properties);
            var saveResult = _configuration.Save();
            if (saveResult.IsError)
                WiitarDebug.Log(saveResult.Error.ToDisplayString(), WiitarDebug.LogLevel.Error);
        }

        int targetId = mode == VirtualOutputMode.VJoyExperimental
            ? Math.Max(1, properties.vJoyDeviceId ?? _configuration.VJoyDeviceId)
            : xInputSlot;
        if (mode == VirtualOutputMode.VJoyExperimental)
        {
            lock (VJoyGate)
            {
                if (!VJoyReservations.Add(targetId))
                {
                    Disconnect();
                    return Result<Unit, VirtualControllerError>.Err(VirtualControllerError.ConnectionFailed(
                        $"vJoy device {targetId} is already used by another guitar. Choose a different vJoy device ID.", targetId));
                }
                _reservedVJoyId = targetId;
            }
        }
        var connectResult = Backend.Connect(targetId, sourceType);
        if (connectResult.IsError)
        {
            Disconnect();
            return connectResult;
        }

        var identityResult = Backend.GetIdentity();
        if (identityResult.IsOk)
            Identity = identityResult.Value;

        var readbackResult = Backend.CreateReadback();
        if (readbackResult.IsOk)
        {
            Readback = readbackResult.Value;
            if (Identity is not null)
            {
                var attachResult = Readback.Attach(Identity);
                if (attachResult.IsError)
                {
                    Readback.Dispose();
                    Readback = null;
                }
            }
        }

        return Result<Unit, VirtualControllerError>.Ok(Unit.Value);
    }

    public Result<ControllerOutputState, VirtualControllerError> Read()
    {
        if (Readback is null)
            return Result<ControllerOutputState, VirtualControllerError>.Err(
                VirtualControllerError.DriverNotReady("Output preview unavailable for the selected backend."));

        try
        {
            return Readback.ReadState();
        }
        catch (InvalidOperationException ex)
        {
            return Result<ControllerOutputState, VirtualControllerError>.Err(
                VirtualControllerError.WriteFailed($"Readback failed: {ex.Message}"));
        }
    }

    public void Disconnect()
    {
        Readback?.Dispose();
        Readback = null;
        if (Backend is not null)
        {
            Backend.Disconnect();
            Backend.Dispose();
        }
        Backend = null;
        lock (VJoyGate)
        {
            if (_reservedVJoyId is int id)
                VJoyReservations.Remove(id);
            _reservedVJoyId = null;
        }
        Identity = null;
        LastReadbackOutput = ControllerOutputState.Empty;
        BackendWriteFailed = false;
    }

    public void Dispose() => Disconnect();

    public bool ShouldKeepWriteErrorInline(VirtualControllerError error)
        => Backend?.Mode == VirtualOutputMode.HidMaestroExperimental
            && (error.Kind == VirtualControllerErrorKind.ConnectionFailed
                || error.Kind == VirtualControllerErrorKind.WriteFailed);
}
