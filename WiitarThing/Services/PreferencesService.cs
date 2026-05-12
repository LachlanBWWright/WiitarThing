using Shared;
using Shared.Windows;

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
                System.Diagnostics.Debug.WriteLine(result.Error.ToDisplayString());
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
            WinBtStream.ForceToshibaMode = !value;
        }
    }

    public VirtualOutputMode VirtualOutputMode => UserPrefs.Instance.virtualOutputMode;

    public Result<Unit, PreferencesError> Save() => UserPrefs.SavePrefs();

    public void ApplyRuntimeSettings()
    {
        WinBtStream.OverrideSharingMode = UserPrefs.Instance.greedyMode;
        if (UserPrefs.Instance.greedyMode)
            WinBtStream.OverridenFileShare = System.IO.FileShare.None;
    }
}
