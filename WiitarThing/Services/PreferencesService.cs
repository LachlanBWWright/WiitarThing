using Shared;

namespace WiinUSoft.Services;

public sealed class PreferencesService : IPreferencesService
{
    public bool AutoStartEnabled
    {
        get => UserPrefs.Instance.autoStartup;
        set
        {
            var result = UserPrefs.SetAutoStart(value);
            if (result.IsError)
                WiitarDebug.Log(result.Error.ToDisplayString(), WiitarDebug.LogLevel.Error);
        }
    }

    public bool StartMinimizedEnabled
    {
        get => UserPrefs.Instance.startMinimized;
        set => UserPrefs.Instance.startMinimized = value;
    }

    public bool ExclusiveBluetoothAccessEnabled
    {
        get => UserPrefs.Instance.greedyMode;
        set
        {
            UserPrefs.Instance.greedyMode = value;
            ApplyRuntimeSettings();
        }
    }

    public bool AutoRefreshEnabled
    {
        get => UserPrefs.Instance.autoRefresh;
        set => UserPrefs.Instance.autoRefresh = value;
    }

    public bool UseMicrosoftBluetoothStack
    {
        get => !UserPrefs.Instance.toshibaMode;
        set
        {
            UserPrefs.Instance.toshibaMode = !value;
        }
    }

    public VirtualOutputMode VirtualOutputMode => UserPrefs.Instance.virtualOutputMode;

    public Result<Unit, PreferencesError> Save() => UserPrefs.SavePrefs();

    public void ApplyRuntimeSettings()
    {
        // Bluetooth preferences are captured when the next connection is created.
    }
}
