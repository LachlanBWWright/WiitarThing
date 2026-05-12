using Shared;

namespace WiinUSoft.Services;

public interface IPreferencesService
{
    bool AutoStartEnabled { get; set; }
    bool StartMinimizedEnabled { get; set; }
    bool ExclusiveBluetoothAccessEnabled { get; set; }
    bool AutoRefreshEnabled { get; set; }
    bool UseMicrosoftBluetoothStack { get; set; }

    VirtualOutputMode VirtualOutputMode { get; }

    Result<Unit, PreferencesError> Save();
    void ApplyRuntimeSettings();
}
