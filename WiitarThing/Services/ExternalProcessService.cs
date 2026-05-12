using System;
using Microsoft.UI.Xaml;
using System.Diagnostics;
using System.IO;

namespace WiinUSoft.Services;

public sealed class ExternalProcessService : IExternalProcessService
{
    public void OpenControllerTestPanel()
    {
        Process.Start(new ProcessStartInfo("joy.cpl") { UseShellExecute = true });
    }

    public void RestartApplicationAndExit()
    {
        string exePath = Path.Combine(AppContext.BaseDirectory, "WiitarThing.exe");
        Process.Start(exePath);
        Application.Current.Exit();
    }
}
