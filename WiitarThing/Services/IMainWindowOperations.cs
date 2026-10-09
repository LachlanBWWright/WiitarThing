using System.Threading.Tasks;

namespace WiinUSoft.Services;

/// <summary>
/// Application operations requested by the main-window view-model.
/// The WinUI window supplies the production implementation; tests can supply
/// a non-visual implementation.
/// </summary>
public interface IMainWindowOperations
{
    Task SynchronizeAsync();
    void DisconnectAllControllers();
    Task RemoveAllWiimotesAsync();
    void OpenControllerTestPanel();
    Task ShowDefaultCalibrationAsync();
    void OpenUiGallery();
    void SetAutoRefresh(bool enabled);
}
