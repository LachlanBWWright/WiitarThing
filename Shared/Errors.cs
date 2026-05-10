#nullable enable
using System;

namespace Shared
{
    // ─── Preferences ────────────────────────────────────────────────────────────

    public enum PreferencesErrorKind
    {
        MissingPath,
        FileNotFound,
        AccessDenied,
        InvalidXml,
        SerializationFailed,
        ValidationFailed,
        Cancelled,
        Unknown
    }

    public sealed class PreferencesError
    {
        public PreferencesErrorKind Kind { get; }
        public string Message { get; }
        public string? Path { get; }
        public Exception? Exception { get; }

        public PreferencesError(PreferencesErrorKind kind, string message, string? path = null, Exception? exception = null)
        {
            Kind = kind;
            Message = message;
            Path = path;
            Exception = exception;
        }

        public override string ToString() => $"PreferencesError({Kind}): {Message}";

        public string ToDisplayString() => Message;

        public static PreferencesError MissingPath() =>
            new PreferencesError(PreferencesErrorKind.MissingPath, "Preferences path is not configured.");

        public static PreferencesError FileNotFound(string path) =>
            new PreferencesError(PreferencesErrorKind.FileNotFound, $"Preferences file not found: {path}", path);

        public static PreferencesError AccessDenied(string path, Exception ex) =>
            new PreferencesError(PreferencesErrorKind.AccessDenied, $"Access denied reading preferences at {path}: {ex.Message}", path, ex);

        public static PreferencesError InvalidXml(string path, Exception ex) =>
            new PreferencesError(PreferencesErrorKind.InvalidXml, $"Malformed XML in preferences at {path}: {ex.Message}", path, ex);

        public static PreferencesError SerializationFailed(string path, Exception ex) =>
            new PreferencesError(PreferencesErrorKind.SerializationFailed, $"Failed to serialise preferences to {path}: {ex.Message}", path, ex);

        public static PreferencesError Unknown(string path, Exception ex) =>
            new PreferencesError(PreferencesErrorKind.Unknown, $"Unexpected error for preferences at {path}: {ex.Message}", path, ex);

        public static PreferencesError ValidationFailed(string message, string? path = null) =>
            new PreferencesError(PreferencesErrorKind.ValidationFailed, message, path);

        public static PreferencesError Cancelled(string message = "Operation cancelled.", string? path = null, Exception? ex = null) =>
            new PreferencesError(PreferencesErrorKind.Cancelled, message, path, ex);
    }

    // ─── Device Discovery ────────────────────────────────────────────────────────

    public enum DeviceDiscoveryErrorKind
    {
        NoneFound,
        AccessDenied,
        DriverNotReady,
        InvalidPath,
        Cancelled,
        ValidationFailed,
        Unknown
    }

    public sealed class DeviceDiscoveryError
    {
        public DeviceDiscoveryErrorKind Kind { get; }
        public string Message { get; }
        public Exception? Exception { get; }

        public DeviceDiscoveryError(DeviceDiscoveryErrorKind kind, string message, Exception? exception = null)
        {
            Kind = kind;
            Message = message;
            Exception = exception;
        }

        public override string ToString() => $"DeviceDiscoveryError({Kind}): {Message}";

        public string ToDisplayString() => Message;

        public static DeviceDiscoveryError NoneFound(string message = "No compatible devices found.") =>
            new DeviceDiscoveryError(DeviceDiscoveryErrorKind.NoneFound, message);

        public static DeviceDiscoveryError AccessDenied(string message, Exception? ex = null) =>
            new DeviceDiscoveryError(DeviceDiscoveryErrorKind.AccessDenied, message, ex);

        public static DeviceDiscoveryError DriverNotReady(string message, Exception? ex = null) =>
            new DeviceDiscoveryError(DeviceDiscoveryErrorKind.DriverNotReady, message, ex);

        public static DeviceDiscoveryError InvalidPath(string message, Exception? ex = null) =>
            new DeviceDiscoveryError(DeviceDiscoveryErrorKind.InvalidPath, message, ex);

        public static DeviceDiscoveryError Cancelled(string message = "Device discovery cancelled.", Exception? ex = null) =>
            new DeviceDiscoveryError(DeviceDiscoveryErrorKind.Cancelled, message, ex);

        public static DeviceDiscoveryError ValidationFailed(string message) =>
            new DeviceDiscoveryError(DeviceDiscoveryErrorKind.ValidationFailed, message);

        public static DeviceDiscoveryError Unknown(string message, Exception? ex = null) =>
            new DeviceDiscoveryError(DeviceDiscoveryErrorKind.Unknown, message, ex);
    }

    // ─── HID / Bluetooth Stream ──────────────────────────────────────────────────

    public enum HidStreamErrorKind
    {
        OpenFailed,
        ReadFailed,
        WriteFailed,
        DeviceDisappeared,
        AccessDenied,
        InvalidPath,
        Cancelled,
        ValidationFailed,
        Unknown
    }

    public sealed class HidStreamError
    {
        public HidStreamErrorKind Kind { get; }
        public string Message { get; }
        public string? DevicePath { get; }
        public Exception? Exception { get; }

        public HidStreamError(HidStreamErrorKind kind, string message, string? devicePath = null, Exception? exception = null)
        {
            Kind = kind;
            Message = message;
            DevicePath = devicePath;
            Exception = exception;
        }

        public override string ToString() => $"HidStreamError({Kind}): {Message}";

        public string ToDisplayString() => Message;

        public static HidStreamError OpenFailed(string message, string? devicePath = null, Exception? ex = null) =>
            new HidStreamError(HidStreamErrorKind.OpenFailed, message, devicePath, ex);

        public static HidStreamError ReadFailed(string message, string? devicePath = null, Exception? ex = null) =>
            new HidStreamError(HidStreamErrorKind.ReadFailed, message, devicePath, ex);

        public static HidStreamError WriteFailed(string message, string? devicePath = null, Exception? ex = null) =>
            new HidStreamError(HidStreamErrorKind.WriteFailed, message, devicePath, ex);

        public static HidStreamError DeviceDisappeared(string message, string? devicePath = null, Exception? ex = null) =>
            new HidStreamError(HidStreamErrorKind.DeviceDisappeared, message, devicePath, ex);

        public static HidStreamError AccessDenied(string message, string? devicePath = null, Exception? ex = null) =>
            new HidStreamError(HidStreamErrorKind.AccessDenied, message, devicePath, ex);

        public static HidStreamError InvalidPath(string message, string? devicePath = null, Exception? ex = null) =>
            new HidStreamError(HidStreamErrorKind.InvalidPath, message, devicePath, ex);

        public static HidStreamError Cancelled(string message = "Stream operation cancelled.", string? devicePath = null, Exception? ex = null) =>
            new HidStreamError(HidStreamErrorKind.Cancelled, message, devicePath, ex);

        public static HidStreamError ValidationFailed(string message, string? devicePath = null) =>
            new HidStreamError(HidStreamErrorKind.ValidationFailed, message, devicePath);

        public static HidStreamError Unknown(string message, string? devicePath = null, Exception? ex = null) =>
            new HidStreamError(HidStreamErrorKind.Unknown, message, devicePath, ex);
    }

    // ─── Controller Parsing ──────────────────────────────────────────────────────

    public enum ControllerParseErrorKind
    {
        PacketTooShort,
        UnknownReportId,
        InvalidData,
        Cancelled,
        ValidationFailed,
        Unknown
    }

    public sealed class ControllerParseError
    {
        public ControllerParseErrorKind Kind { get; }
        public string Message { get; }
        public Exception? Exception { get; }

        public ControllerParseError(ControllerParseErrorKind kind, string message, Exception? exception = null)
        {
            Kind = kind;
            Message = message;
            Exception = exception;
        }

        public override string ToString() => $"ControllerParseError({Kind}): {Message}";

        public string ToDisplayString() => Message;

        public static ControllerParseError PacketTooShort(string message, Exception? ex = null) =>
            new ControllerParseError(ControllerParseErrorKind.PacketTooShort, message, ex);

        public static ControllerParseError UnknownReportId(string message, Exception? ex = null) =>
            new ControllerParseError(ControllerParseErrorKind.UnknownReportId, message, ex);

        public static ControllerParseError InvalidData(string message, Exception? ex = null) =>
            new ControllerParseError(ControllerParseErrorKind.InvalidData, message, ex);

        public static ControllerParseError Cancelled(string message = "Controller parsing cancelled.", Exception? ex = null) =>
            new ControllerParseError(ControllerParseErrorKind.Cancelled, message, ex);

        public static ControllerParseError ValidationFailed(string message) =>
            new ControllerParseError(ControllerParseErrorKind.ValidationFailed, message);

        public static ControllerParseError Unknown(string message, Exception? ex = null) =>
            new ControllerParseError(ControllerParseErrorKind.Unknown, message, ex);
    }

    // ─── Virtual Controller ──────────────────────────────────────────────────────

    public enum VirtualControllerErrorKind
    {
        SlotUnavailable,
        ConnectionFailed,
        DriverNotReady,
        WriteFailed,
        InvalidMapping,
        Cancelled,
        Unknown
    }

    public sealed class VirtualControllerError
    {
        public VirtualControllerErrorKind Kind { get; }
        public string Message { get; }
        public int? RequestedSlot { get; }
        public Exception? Exception { get; }

        public VirtualControllerError(VirtualControllerErrorKind kind, string message, int? requestedSlot = null, Exception? exception = null)
        {
            Kind = kind;
            Message = message;
            RequestedSlot = requestedSlot;
            Exception = exception;
        }

        public override string ToString() => $"VirtualControllerError({Kind}): {Message}";

        public string ToDisplayString() => Message;

        public static VirtualControllerError SlotUnavailable(int? slot = null, Exception? ex = null) =>
            new VirtualControllerError(VirtualControllerErrorKind.SlotUnavailable, "Virtual controller slot is unavailable.", slot, ex);

        public static VirtualControllerError ConnectionFailed(string message, int? slot = null, Exception? ex = null) =>
            new VirtualControllerError(VirtualControllerErrorKind.ConnectionFailed, message, slot, ex);

        public static VirtualControllerError DriverNotReady(string message, int? slot = null, Exception? ex = null) =>
            new VirtualControllerError(VirtualControllerErrorKind.DriverNotReady, message, slot, ex);

        public static VirtualControllerError WriteFailed(string message, int? slot = null, Exception? ex = null) =>
            new VirtualControllerError(VirtualControllerErrorKind.WriteFailed, message, slot, ex);

        public static VirtualControllerError InvalidMapping(string message, int? slot = null) =>
            new VirtualControllerError(VirtualControllerErrorKind.InvalidMapping, message, slot);

        public static VirtualControllerError Cancelled(string message = "Virtual controller operation cancelled.", int? slot = null, Exception? ex = null) =>
            new VirtualControllerError(VirtualControllerErrorKind.Cancelled, message, slot, ex);

        public static VirtualControllerError Unknown(string message, int? slot = null, Exception? ex = null) =>
            new VirtualControllerError(VirtualControllerErrorKind.Unknown, message, slot, ex);
    }

    // ─── Calibration ─────────────────────────────────────────────────────────────

    public enum CalibrationErrorKind
    {
        InvalidString,
        ParseFailed,
        ValidationFailed,
        Cancelled,
        Unknown
    }

    public sealed class CalibrationError
    {
        public CalibrationErrorKind Kind { get; }
        public string Message { get; }
        public Exception? Exception { get; }

        public CalibrationError(CalibrationErrorKind kind, string message, Exception? exception = null)
        {
            Kind = kind;
            Message = message;
            Exception = exception;
        }

        public override string ToString() => $"CalibrationError({Kind}): {Message}";

        public string ToDisplayString() => Message;

        public static CalibrationError InvalidString(string message, Exception? ex = null) =>
            new CalibrationError(CalibrationErrorKind.InvalidString, message, ex);

        public static CalibrationError ParseFailed(string message, Exception? ex = null) =>
            new CalibrationError(CalibrationErrorKind.ParseFailed, message, ex);

        public static CalibrationError ValidationFailed(string message) =>
            new CalibrationError(CalibrationErrorKind.ValidationFailed, message);

        public static CalibrationError Cancelled(string message = "Calibration cancelled.", Exception? ex = null) =>
            new CalibrationError(CalibrationErrorKind.Cancelled, message, ex);

        public static CalibrationError Unknown(string message, Exception? ex = null) =>
            new CalibrationError(CalibrationErrorKind.Unknown, message, ex);
    }

    // ─── Bluetooth Lookup ────────────────────────────────────────────────────────

    public enum BluetoothErrorKind
    {
        DcidNotFound,
        ScidNotFound,
        ConnectionNotFound,
        Cancelled,
        ValidationFailed,
        Unknown
    }

    public sealed class BluetoothError
    {
        public BluetoothErrorKind Kind { get; }
        public string Message { get; }
        public Exception? Exception { get; }

        public BluetoothError(BluetoothErrorKind kind, string message, Exception? exception = null)
        {
            Kind = kind;
            Message = message;
            Exception = exception;
        }

        public override string ToString() => $"BluetoothError({Kind}): {Message}";

        public string ToDisplayString() => Message;

        public static BluetoothError DcidNotFound(string message, Exception? ex = null) =>
            new BluetoothError(BluetoothErrorKind.DcidNotFound, message, ex);

        public static BluetoothError ScidNotFound(string message, Exception? ex = null) =>
            new BluetoothError(BluetoothErrorKind.ScidNotFound, message, ex);

        public static BluetoothError ConnectionNotFound(string message, Exception? ex = null) =>
            new BluetoothError(BluetoothErrorKind.ConnectionNotFound, message, ex);

        public static BluetoothError Cancelled(string message = "Bluetooth operation cancelled.", Exception? ex = null) =>
            new BluetoothError(BluetoothErrorKind.Cancelled, message, ex);

        public static BluetoothError ValidationFailed(string message) =>
            new BluetoothError(BluetoothErrorKind.ValidationFailed, message);

        public static BluetoothError Unknown(string message, Exception? ex = null) =>
            new BluetoothError(BluetoothErrorKind.Unknown, message, ex);
    }
}
