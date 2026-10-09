using System.Diagnostics;
using System.IO;
using NintrollerLib;
using Shared;
using Shared.Windows;
using WiinUSoft.ViewModels;

namespace WiinUSoft.Services;

/// <summary>Creates the hardware session and its view-model outside the window.</summary>
public sealed class DeviceViewModelFactory
{
    private readonly IDeviceConfigurationService _configuration;
    private readonly IUiDispatcher _uiDispatcher;

    public DeviceViewModelFactory(IDeviceConfigurationService configuration, IUiDispatcher uiDispatcher)
    {
        _configuration = configuration ?? throw new System.ArgumentNullException(nameof(configuration));
        _uiDispatcher = uiDispatcher ?? throw new System.ArgumentNullException(nameof(uiDispatcher));
    }

    public DeviceViewModel? Create(DeviceInfo device, IUserNotificationService? notifications = null)
    {
        var stream = new WinBtStream(
            device.DevicePath,
            UserPrefs.Instance.toshibaMode ? WinBtStream.BtStack.Toshiba : WinBtStream.BtStack.Microsoft,
            UserPrefs.Instance.greedyMode ? FileShare.None : FileShare.ReadWrite);
        var controller = new Nintroller(stream, device.Type);
        var openResult = stream.TryOpenConnection();
        if (openResult.IsError || !stream.CanRead)
        {
            if (openResult.IsError)
                WiitarDebug.Log(openResult.Error.ToDisplayString(), WiitarDebug.LogLevel.Error);
            controller.Dispose();
            return null;
        }

        var session = new DeviceSession(controller, device.DevicePath, _configuration, notifications);
        session.Refresh();
        return new DeviceViewModel(session, _configuration, _uiDispatcher);
    }
}
