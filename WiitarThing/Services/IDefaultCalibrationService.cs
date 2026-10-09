using System.Collections.Generic;
using Shared;

namespace WiinUSoft.Services;

public interface IDefaultCalibrationService
{
    Property? DefaultProperty { get; }
    IEnumerable<Property> DevicePreferences { get; }
    Result<Unit, PreferencesError> SaveDefault(Property property);
    Result<Unit, PreferencesError> ClearDefault();
}

public sealed class DefaultCalibrationService : IDefaultCalibrationService
{
    public Property? DefaultProperty => UserPrefs.Instance.defaultProperty;
    public IEnumerable<Property> DevicePreferences => UserPrefs.Instance.devicePrefs;

    public Result<Unit, PreferencesError> SaveDefault(Property property)
    {
        UserPrefs.Instance.defaultProperty = property;
        return UserPrefs.SavePrefs();
    }

    public Result<Unit, PreferencesError> ClearDefault()
    {
        if (UserPrefs.Instance.defaultProperty is not null)
            UserPrefs.Instance.devicePrefs.Remove(UserPrefs.Instance.defaultProperty);
        UserPrefs.Instance.defaultProfile = null;
        return UserPrefs.SavePrefs();
    }
}
