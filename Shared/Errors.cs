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
    }

    // ─── Device Discovery ────────────────────────────────────────────────────────

    public enum DeviceDiscoveryErrorKind
    {
        NoneFound,
        AccessDenied,
        DriverNotReady,
        InvalidPath,
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
    }

    // ─── Controller Parsing ──────────────────────────────────────────────────────

    public enum ControllerParseErrorKind
    {
        PacketTooShort,
        UnknownReportId,
        InvalidData,
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
    }

    // ─── Virtual Controller ──────────────────────────────────────────────────────

    public enum VirtualControllerErrorKind
    {
        SlotUnavailable,
        ConnectionFailed,
        DriverNotReady,
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
    }

    // ─── Calibration ─────────────────────────────────────────────────────────────

    public enum CalibrationErrorKind
    {
        InvalidString,
        ParseFailed,
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
    }

    // ─── Bluetooth Lookup ────────────────────────────────────────────────────────

    public enum BluetoothErrorKind
    {
        DcidNotFound,
        ScidNotFound,
        ConnectionNotFound,
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
    }
}
