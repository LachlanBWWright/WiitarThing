#nullable enable
using System;

namespace Shared
{
    // Preferences

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

    public sealed record PreferencesError(
        PreferencesErrorKind Kind,
        string Message,
        string? Path = null,
        Exception? Exception = null)
    {
        public override string ToString() => $"PreferencesError({Kind}): {Message}";

        public string ToDisplayString() => Message;

        public static PreferencesError MissingPath() =>
            new(PreferencesErrorKind.MissingPath, "Preferences path is not configured.");

        public static PreferencesError FileNotFound(string path) =>
            new(PreferencesErrorKind.FileNotFound, $"Preferences file not found: {path}", path);

        public static PreferencesError AccessDenied(string path, Exception ex) =>
            new(PreferencesErrorKind.AccessDenied, $"Access denied reading preferences at {path}: {ex.Message}", path, ex);

        public static PreferencesError InvalidXml(string path, Exception ex) =>
            new(PreferencesErrorKind.InvalidXml, $"Malformed XML in preferences at {path}: {ex.Message}", path, ex);

        public static PreferencesError SerializationFailed(string path, Exception ex) =>
            new(PreferencesErrorKind.SerializationFailed, $"Failed to serialise preferences to {path}: {ex.Message}", path, ex);

        public static PreferencesError Unknown(string path, Exception ex) =>
            new(PreferencesErrorKind.Unknown, $"Unexpected error for preferences at {path}: {ex.Message}", path, ex);

        public static PreferencesError ValidationFailed(string message, string? path = null) =>
            new(PreferencesErrorKind.ValidationFailed, message, path);

        public static PreferencesError Cancelled(string message = "Operation cancelled.", string? path = null, Exception? ex = null) =>
            new(PreferencesErrorKind.Cancelled, message, path, ex);
    }

    // Device Discovery

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

    public sealed record DeviceDiscoveryError(
        DeviceDiscoveryErrorKind Kind,
        string Message,
        Exception? Exception = null)
    {
        public override string ToString() => $"DeviceDiscoveryError({Kind}): {Message}";

        public string ToDisplayString() => Message;

        public static DeviceDiscoveryError NoneFound(string message = "No compatible devices found.") =>
            new(DeviceDiscoveryErrorKind.NoneFound, message);

        public static DeviceDiscoveryError AccessDenied(string message, Exception? ex = null) =>
            new(DeviceDiscoveryErrorKind.AccessDenied, message, ex);

        public static DeviceDiscoveryError DriverNotReady(string message, Exception? ex = null) =>
            new(DeviceDiscoveryErrorKind.DriverNotReady, message, ex);

        public static DeviceDiscoveryError InvalidPath(string message, Exception? ex = null) =>
            new(DeviceDiscoveryErrorKind.InvalidPath, message, ex);

        public static DeviceDiscoveryError Cancelled(string message = "Device discovery cancelled.", Exception? ex = null) =>
            new(DeviceDiscoveryErrorKind.Cancelled, message, ex);

        public static DeviceDiscoveryError ValidationFailed(string message) =>
            new(DeviceDiscoveryErrorKind.ValidationFailed, message);

        public static DeviceDiscoveryError Unknown(string message, Exception? ex = null) =>
            new(DeviceDiscoveryErrorKind.Unknown, message, ex);
    }

    // HID / Bluetooth Stream

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

    public sealed record HidStreamError(
        HidStreamErrorKind Kind,
        string Message,
        string? DevicePath = null,
        Exception? Exception = null)
    {
        public override string ToString() => $"HidStreamError({Kind}): {Message}";

        public string ToDisplayString() => Message;

        public static HidStreamError OpenFailed(string message, string? devicePath = null, Exception? ex = null) =>
            new(HidStreamErrorKind.OpenFailed, message, devicePath, ex);

        public static HidStreamError ReadFailed(string message, string? devicePath = null, Exception? ex = null) =>
            new(HidStreamErrorKind.ReadFailed, message, devicePath, ex);

        public static HidStreamError WriteFailed(string message, string? devicePath = null, Exception? ex = null) =>
            new(HidStreamErrorKind.WriteFailed, message, devicePath, ex);

        public static HidStreamError DeviceDisappeared(string message, string? devicePath = null, Exception? ex = null) =>
            new(HidStreamErrorKind.DeviceDisappeared, message, devicePath, ex);

        public static HidStreamError AccessDenied(string message, string? devicePath = null, Exception? ex = null) =>
            new(HidStreamErrorKind.AccessDenied, message, devicePath, ex);

        public static HidStreamError InvalidPath(string message, string? devicePath = null, Exception? ex = null) =>
            new(HidStreamErrorKind.InvalidPath, message, devicePath, ex);

        public static HidStreamError Cancelled(string message = "Stream operation cancelled.", string? devicePath = null, Exception? ex = null) =>
            new(HidStreamErrorKind.Cancelled, message, devicePath, ex);

        public static HidStreamError ValidationFailed(string message, string? devicePath = null) =>
            new(HidStreamErrorKind.ValidationFailed, message, devicePath);

        public static HidStreamError Unknown(string message, string? devicePath = null, Exception? ex = null) =>
            new(HidStreamErrorKind.Unknown, message, devicePath, ex);
    }

    // Controller Parsing

    public enum ControllerParseErrorKind
    {
        PacketTooShort,
        UnknownReportId,
        InvalidData,
        Cancelled,
        ValidationFailed,
        Unknown
    }

    public sealed record ControllerParseError(
        ControllerParseErrorKind Kind,
        string Message,
        Exception? Exception = null)
    {
        public override string ToString() => $"ControllerParseError({Kind}): {Message}";

        public string ToDisplayString() => Message;

        public static ControllerParseError PacketTooShort(string message, Exception? ex = null) =>
            new(ControllerParseErrorKind.PacketTooShort, message, ex);

        public static ControllerParseError UnknownReportId(string message, Exception? ex = null) =>
            new(ControllerParseErrorKind.UnknownReportId, message, ex);

        public static ControllerParseError InvalidData(string message, Exception? ex = null) =>
            new(ControllerParseErrorKind.InvalidData, message, ex);

        public static ControllerParseError Cancelled(string message = "Controller parsing cancelled.", Exception? ex = null) =>
            new(ControllerParseErrorKind.Cancelled, message, ex);

        public static ControllerParseError ValidationFailed(string message) =>
            new(ControllerParseErrorKind.ValidationFailed, message);

        public static ControllerParseError Unknown(string message, Exception? ex = null) =>
            new(ControllerParseErrorKind.Unknown, message, ex);
    }

    // Virtual Controller

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

    public sealed record VirtualControllerError(
        VirtualControllerErrorKind Kind,
        string Message,
        int? RequestedSlot = null,
        Exception? Exception = null)
    {
        public override string ToString() => $"VirtualControllerError({Kind}): {Message}";

        public string ToDisplayString() => Message;

        public static VirtualControllerError SlotUnavailable(int? slot = null, Exception? ex = null) =>
            new(VirtualControllerErrorKind.SlotUnavailable, "Virtual controller slot is unavailable.", slot, ex);

        public static VirtualControllerError ConnectionFailed(string message, int? slot = null, Exception? ex = null) =>
            new(VirtualControllerErrorKind.ConnectionFailed, message, slot, ex);

        public static VirtualControllerError DriverNotReady(string message, int? slot = null, Exception? ex = null) =>
            new(VirtualControllerErrorKind.DriverNotReady, message, slot, ex);

        public static VirtualControllerError WriteFailed(string message, int? slot = null, Exception? ex = null) =>
            new(VirtualControllerErrorKind.WriteFailed, message, slot, ex);

        public static VirtualControllerError InvalidMapping(string message, int? slot = null) =>
            new(VirtualControllerErrorKind.InvalidMapping, message, slot);

        public static VirtualControllerError Cancelled(string message = "Virtual controller operation cancelled.", int? slot = null, Exception? ex = null) =>
            new(VirtualControllerErrorKind.Cancelled, message, slot, ex);

        public static VirtualControllerError Unknown(string message, int? slot = null, Exception? ex = null) =>
            new(VirtualControllerErrorKind.Unknown, message, slot, ex);
    }

    // Calibration

    public enum CalibrationErrorKind
    {
        InvalidString,
        ParseFailed,
        ValidationFailed,
        Cancelled,
        Unknown
    }

    public sealed record CalibrationError(
        CalibrationErrorKind Kind,
        string Message,
        Exception? Exception = null)
    {
        public override string ToString() => $"CalibrationError({Kind}): {Message}";

        public string ToDisplayString() => Message;

        public static CalibrationError InvalidString(string message, Exception? ex = null) =>
            new(CalibrationErrorKind.InvalidString, message, ex);

        public static CalibrationError ParseFailed(string message, Exception? ex = null) =>
            new(CalibrationErrorKind.ParseFailed, message, ex);

        public static CalibrationError ValidationFailed(string message) =>
            new(CalibrationErrorKind.ValidationFailed, message);

        public static CalibrationError Cancelled(string message = "Calibration cancelled.", Exception? ex = null) =>
            new(CalibrationErrorKind.Cancelled, message, ex);

        public static CalibrationError Unknown(string message, Exception? ex = null) =>
            new(CalibrationErrorKind.Unknown, message, ex);
    }

    // Bluetooth Lookup

    public enum BluetoothErrorKind
    {
        DcidNotFound,
        ScidNotFound,
        ConnectionNotFound,
        Cancelled,
        ValidationFailed,
        Unknown
    }

    public sealed record BluetoothError(
        BluetoothErrorKind Kind,
        string Message,
        Exception? Exception = null)
    {
        public override string ToString() => $"BluetoothError({Kind}): {Message}";

        public string ToDisplayString() => Message;

        public static BluetoothError DcidNotFound(string message, Exception? ex = null) =>
            new(BluetoothErrorKind.DcidNotFound, message, ex);

        public static BluetoothError ScidNotFound(string message, Exception? ex = null) =>
            new(BluetoothErrorKind.ScidNotFound, message, ex);

        public static BluetoothError ConnectionNotFound(string message, Exception? ex = null) =>
            new(BluetoothErrorKind.ConnectionNotFound, message, ex);

        public static BluetoothError Cancelled(string message = "Bluetooth operation cancelled.", Exception? ex = null) =>
            new(BluetoothErrorKind.Cancelled, message, ex);

        public static BluetoothError ValidationFailed(string message) =>
            new(BluetoothErrorKind.ValidationFailed, message);

        public static BluetoothError Unknown(string message, Exception? ex = null) =>
            new(BluetoothErrorKind.Unknown, message, ex);
    }
}
