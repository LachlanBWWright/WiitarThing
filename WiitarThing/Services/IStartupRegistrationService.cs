using Shared;

namespace WiinUSoft.Services;

public interface IStartupRegistrationService
{
    Result<Unit, PreferencesError> CreateStartupShortcut(string startupFolder);
}
