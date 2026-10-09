using System.Media;

namespace WiinUSoft.Services;

public interface IUserNotificationService
{
    bool IsVisible { get; }
    void ShowBalloon(string title, string message, int icon = 0, SystemSound? sound = null);
}

internal sealed class TrayNotificationService : IUserNotificationService
{
    private readonly TrayIconService _tray;

    public TrayNotificationService(TrayIconService tray) => _tray = tray;

    public bool IsVisible => _tray.IsVisible;

    public void ShowBalloon(string title, string message, int icon = 0, SystemSound? sound = null)
    {
        _tray.ShowBalloon(title, message, icon);
        sound?.Play();
    }
}
