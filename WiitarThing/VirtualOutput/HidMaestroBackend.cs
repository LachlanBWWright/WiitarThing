using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using HIDMaestro;
using NintrollerLib;
using Shared;

namespace WiinUSoft.VirtualOutput;

internal sealed class HidMaestroBackend : IVirtualControllerBackend
{
    private const string HIDMAESTRO_CORE_DLL = "HIDMaestro.Core.dll";
    private const string DefaultProfileId = "xbox-360-guitar-v2";

    private HMContext? _context;
    private HMProfile? _profile;
    private HMController? _controller;
    private int _controllerIndex;

    public string DisplayName => "Virtual Guitar (HIDMaestro)";
    public VirtualOutputMode Mode => VirtualOutputMode.HidMaestroExperimental;

    public static bool IsRuntimeAvailable()
    {
        try
        {
            using var context = new HMContext();
            return context.IsDriverInstalled;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
        catch (Exception)
        {
            return FindHidMaestroPath() != null;
        }
    }

    public Result<Unit, VirtualControllerError> Connect(int slotOrDeviceId, ControllerType sourceType)
    {
        if (sourceType != ControllerType.Guitar)
        {
            return Result<Unit, VirtualControllerError>.Err(
                VirtualControllerError.InvalidMapping("HIDMaestro backend supports guitar output only.", slotOrDeviceId > 0 ? slotOrDeviceId : null));
        }

        try
        {
            _context = new HMContext();
            if (!_context.IsDriverInstalled)
            {
                try
                {
                    _context.InstallDriver();
                }
                catch (UnauthorizedAccessException ex)
                {
                    return Result<Unit, VirtualControllerError>.Err(
                        VirtualControllerError.DriverNotReady(
                            "HIDMaestro is bundled, but its driver is not installed. Restart WiitarThing as administrator and select HIDMaestro again to install it.",
                            slotOrDeviceId,
                            ex));
                }
                catch (CryptographicException ex) when (IsAccessDenied(ex))
                {
                    return Result<Unit, VirtualControllerError>.Err(
                        VirtualControllerError.DriverNotReady(
                            "HIDMaestro is bundled, but Windows denied access while installing its embedded driver certificate. Restart WiitarThing as administrator and select HIDMaestro again.",
                            slotOrDeviceId,
                            ex));
                }
            }

            int loaded = _context.LoadDefaultProfiles();
            if (loaded == 0 && _context.AllProfiles.Count == 0)
            {
                return Result<Unit, VirtualControllerError>.Err(
                    VirtualControllerError.DriverNotReady("HIDMaestro loaded, but no embedded controller profiles were available.", slotOrDeviceId));
            }

            _profile = _context.GetProfile(DefaultProfileId);
            if (_profile == null)
            {
                return Result<Unit, VirtualControllerError>.Err(
                    VirtualControllerError.InvalidMapping($"HIDMaestro profile '{DefaultProfileId}' was not found.", slotOrDeviceId));
            }

            int controllerIndex = Math.Max(0, slotOrDeviceId - 1);
            _controller = _context.CreateControllerAt(controllerIndex, _profile);
            _controllerIndex = controllerIndex;
            _context.FinalizeNames();
            return Result<Unit, VirtualControllerError>.Ok(Unit.Value);
        }
        catch (FileNotFoundException ex)
        {
            return Result<Unit, VirtualControllerError>.Err(
                VirtualControllerError.DriverNotReady("HIDMaestro.Core.dll was not found. Bundle it under Drivers\\HIDMaestro or next to WiitarThing.", slotOrDeviceId, ex));
        }
        catch (BadImageFormatException ex)
        {
            return Result<Unit, VirtualControllerError>.Err(
                VirtualControllerError.DriverNotReady("HIDMaestro.Core.dll is not compatible with this process architecture. Use the x64 HIDMaestro build.", slotOrDeviceId, ex));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result<Unit, VirtualControllerError>.Err(
                VirtualControllerError.DriverNotReady("HIDMaestro virtual controller creation requires administrator privileges.", slotOrDeviceId, ex));
        }
        catch (CryptographicException ex) when (IsAccessDenied(ex))
        {
            return Result<Unit, VirtualControllerError>.Err(
                VirtualControllerError.DriverNotReady("HIDMaestro driver setup requires administrator privileges to install or trust its embedded signing certificate.", slotOrDeviceId, ex));
        }
        catch (InvalidOperationException ex) when (IsAccessDenied(ex))
        {
            return Result<Unit, VirtualControllerError>.Err(
                VirtualControllerError.DriverNotReady("HIDMaestro driver setup was denied by Windows. Restart WiitarThing as administrator and try the HIDMaestro backend again.", slotOrDeviceId, ex));
        }
        catch (Exception ex)
        {
            return Result<Unit, VirtualControllerError>.Err(
                VirtualControllerError.ConnectionFailed($"HIDMaestro connection failed: {ex.Message}", slotOrDeviceId, ex));
        }
    }

    public Result<Unit, VirtualControllerError> Update(ControllerOutputState state)
    {
        if (_controller == null || _profile == null)
        {
            return Result<Unit, VirtualControllerError>.Err(
                VirtualControllerError.ConnectionFailed("HIDMaestro virtual controller is not connected."));
        }

        try
        {
            _controller.SubmitState(ToHidMaestroState(_profile, state));
            return Result<Unit, VirtualControllerError>.Ok(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, VirtualControllerError>.Err(
                VirtualControllerError.WriteFailed($"HIDMaestro update failed: {ex.Message}", ex: ex));
        }
    }

    public Result<Unit, VirtualControllerError> Disconnect()
    {
        try
        {
            _controller?.Dispose();
        }
        catch { }

        try
        {
            _context?.Dispose();
        }
        catch { }

        _controller = null;
        _profile = null;
        _context = null;
        _controllerIndex = 0;
        return Result<Unit, VirtualControllerError>.Ok(Unit.Value);
    }

    public Result<VirtualControllerIdentity, VirtualControllerError> GetIdentity()
    {
        if (_controller == null || _profile == null)
        {
            return Result<VirtualControllerIdentity, VirtualControllerError>.Err(
                VirtualControllerError.ConnectionFailed("HIDMaestro virtual controller is not connected."));
        }

        return Result<VirtualControllerIdentity, VirtualControllerError>.Ok(
            new VirtualControllerIdentity(
                DisplayName,
                XInputSlot: _controllerIndex + 1,
                VendorId: _profile.VendorId,
                ProductId: _profile.ProductId,
                ProductName: _profile.ProductString,
                InstanceId: $"HIDMaestro:{DefaultProfileId}:{_controllerIndex}"));
    }

    public Result<IVirtualControllerReadback, VirtualControllerError> CreateReadback()
    {
        if (_controller == null)
        {
            return Result<IVirtualControllerReadback, VirtualControllerError>.Err(
                VirtualControllerError.ConnectionFailed("HIDMaestro virtual controller is not connected."));
        }

        return Result<IVirtualControllerReadback, VirtualControllerError>.Ok(new XInputReadback());
    }

    public void Dispose()
    {
        Disconnect();
    }

    internal static string? FindHidMaestroPath()
    {
        foreach (string path in GetHidMaestroSearchPaths())
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static HMGamepadState ToHidMaestroState(HMProfile profile, ControllerOutputState state)
    {
        HMButton buttons = HMButton.None;
        if (state.Green) buttons |= HMButton.A;
        if (state.Red) buttons |= HMButton.B;
        if (state.Yellow) buttons |= HMButton.X;
        if (state.Blue) buttons |= HMButton.Y;
        if (state.Orange) buttons |= HMButton.LeftBumper;
        if (state.Start) buttons |= HMButton.Start;
        if (state.Select) buttons |= HMButton.Back;
        if (state.Home) buttons |= HMButton.Guide;

        var axes = new Dictionary<HMAxis, float>();
        var guitarLayout = profile.AsGuitar();
        if (guitarLayout?.WhammyAxis is HMAxis whammyAxis && whammyAxis != HMAxis.None)
        {
            axes[whammyAxis] = ToCenteredAxis(state.Whammy);
        }
        else
        {
            axes = HMGamepadStateHelpers.StandardAxes(
                profile,
                rightStickX: ToCenteredAxis(state.Whammy),
                rightStickY: ToCenteredAxis(state.Tilt));
        }

        return new HMGamepadState
        {
            Buttons = buttons,
            Hat = ToHat(state),
            Axes = axes
        };
    }

    private static float ToCenteredAxis(float value)
    {
        return (ControllerOutputState.ClampSigned(value) + 1f) * 0.5f;
    }

    private static HMHat ToHat(ControllerOutputState state)
    {
        if (state.StrumUp && state.DPadRight) return HMHat.NorthEast;
        if (state.StrumUp && state.DPadLeft) return HMHat.NorthWest;
        if (state.StrumDown && state.DPadRight) return HMHat.SouthEast;
        if (state.StrumDown && state.DPadLeft) return HMHat.SouthWest;
        if (state.StrumUp) return HMHat.North;
        if (state.StrumDown) return HMHat.South;
        if (state.DPadRight) return HMHat.East;
        if (state.DPadLeft) return HMHat.West;
        return HMHat.None;
    }

    private static bool IsAccessDenied(Exception ex)
    {
        const int E_ACCESSDENIED = unchecked((int)0x80070005);
        return ex.HResult == E_ACCESSDENIED
            || ex.Message.Contains("access is denied", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("access denied", StringComparison.OrdinalIgnoreCase)
            || (ex.InnerException != null && IsAccessDenied(ex.InnerException));
    }

    private static string[] GetHidMaestroSearchPaths()
    {
        var paths = new System.Collections.Generic.List<string>();
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; current != null && i < 8; i++)
        {
            paths.Add(Path.Combine(current.FullName, HIDMAESTRO_CORE_DLL));
            paths.Add(Path.Combine(current.FullName, "Drivers", "HIDMaestro", HIDMAESTRO_CORE_DLL));
            paths.Add(Path.Combine(current.FullName, "HIDMaestro", HIDMAESTRO_CORE_DLL));
            current = current.Parent;
        }

        return paths.ToArray();
    }

}
