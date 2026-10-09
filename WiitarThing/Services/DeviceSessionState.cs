namespace WiinUSoft.Services;

/// <summary>
/// Non-visual state owned by a device session. Keeping this state outside the
/// XAML control makes the transport/session boundary explicit and testable.
/// </summary>
internal sealed class DeviceSessionState
{
    public string DevicePath { get; set; } = string.Empty;
    public DeviceState ConnectionState { get; set; }
    public Property Properties { get; set; } = null!;
    public int TargetXDevice { get; set; }
    public bool LowBatteryFired { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}
