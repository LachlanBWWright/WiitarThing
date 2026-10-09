using Shared;
using NintrollerLib;

namespace WiinUSoft.Services;

/// <summary>Application-facing device configuration boundary.</summary>
public interface IDeviceConfigurationService
{
    DevicePreferencesSnapshot LoadDevice(string devicePath, ControllerType controllerType);
    Property? GetDevicePreference(string devicePath);
    Profile? DefaultProfile { get; }
    VirtualOutputMode VirtualOutputMode { get; set; }
    GuitarPreviewMode GuitarPreviewMode { get; set; }
    int VJoyDeviceId { get; }
    void AddDevicePreference(Property property);
    void UpdateDeviceIcon(string devicePath, string iconKey);
    Result<Unit, PreferencesError> Save();
}

public sealed record DevicePreferencesSnapshot(Property Properties, string DisplayName, bool SnapIrPointer, string? IconKey);

public sealed class DeviceConfigurationService : IDeviceConfigurationService
{
    public DevicePreferencesSnapshot LoadDevice(string devicePath, ControllerType controllerType)
    {
        var properties = GetDevicePreference(devicePath);
        if (properties is null)
        {
            var defaults = UserPrefs.Instance.defaultProperty;
            properties = defaults is null ? new Property(devicePath) : new Property(defaults) { hid = devicePath };
        }
        // Keep one preference object per device so sessions and editors see the same settings.
        UserPrefs.Instance.AddDevicePref(properties);
        var displayName = string.IsNullOrWhiteSpace(properties.name) ? controllerType.ToString() : properties.name;
        var snapIrPointer = properties.pointerMode != Property.PointerOffScreenMode.Center;
        return new DevicePreferencesSnapshot(properties, displayName, snapIrPointer, properties.lastIcon);
    }

    public Property? GetDevicePreference(string devicePath) => UserPrefs.Instance.devicePrefs.Find(pref => pref.hid == devicePath);
    public Profile? DefaultProfile => UserPrefs.Instance.defaultProfile;
    public VirtualOutputMode VirtualOutputMode { get => UserPrefs.Instance.virtualOutputMode; set => UserPrefs.Instance.virtualOutputMode = value; }
    public GuitarPreviewMode GuitarPreviewMode { get => UserPrefs.Instance.guitarPreviewMode; set => UserPrefs.Instance.guitarPreviewMode = value; }
    public int VJoyDeviceId => UserPrefs.Instance.vJoyDeviceId;
    public void AddDevicePreference(Property property) => UserPrefs.Instance.AddDevicePref(property);
    public void UpdateDeviceIcon(string devicePath, string iconKey) => UserPrefs.Instance.UpdateDeviceIcon(devicePath, iconKey);
    public Result<Unit, PreferencesError> Save() => UserPrefs.SavePrefs();
}
