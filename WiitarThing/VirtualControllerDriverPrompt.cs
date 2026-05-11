using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ScpControl;
using WiinUSoft.Holders;

namespace WiinUSoft
{
    internal static class VirtualControllerDriverPrompt
    {
        private static bool _startupPromptShown;
        private static bool _dialogOpen;

        public static bool IsDriverAvailable()
        {
            try
            {
                return XBus.Default.State == DsState.Connected;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }

        public static async Task CheckAtStartupAsync()
        {
            if (_startupPromptShown || IsDriverAvailable()) return;

            XamlRoot? xamlRoot = await WaitForXamlRootAsync();
            if (xamlRoot == null) return;

            _startupPromptShown = true;
            await PromptInstallAsync(xamlRoot, startupCheck: true);
        }

        public static async Task<bool> PromptInstallAsync(XamlRoot? xamlRoot, bool startupCheck = false)
        {
            if (_dialogOpen) return false;

            xamlRoot = TryResolveXamlRoot(xamlRoot);
            if (xamlRoot == null) return false;

            string? installerPath = FindInstallerPath();
            string content = installerPath == null
                ? "The SCP virtual bus driver is not installed, and the bundled SCP driver installer was not found. Reinstall WiitarThing or place the SCP driver files next to the application."
                : "The SCP virtual bus driver is not installed or is not available. WiitarThing needs it to create virtual Xbox 360 controllers.";

            var dlg = new ContentDialog
            {
                Title = "Virtual Xbox Driver Required",
                Content = content,
                PrimaryButtonText = "Install Driver",
                IsPrimaryButtonEnabled = installerPath != null,
                SecondaryButtonText = "Retry",
                CloseButtonText = startupCheck ? "Later" : "Cancel",
                XamlRoot = xamlRoot
            };

            _dialogOpen = true;
            ContentDialogResult result;
            try
            {
                result = await dlg.ShowAsync();
            }
            finally
            {
                _dialogOpen = false;
            }

            if (result == ContentDialogResult.Secondary)
                return IsDriverAvailable();

            if (result != ContentDialogResult.Primary || installerPath == null)
                return false;

            if (!await LaunchInstallerAsync(installerPath, xamlRoot))
                return false;

            if (IsDriverAvailable())
                return true;

            var restartDlg = new ContentDialog
            {
                Title = "Driver Install Started",
                Content = "If the SCP driver installer completed successfully, restart WiitarThing as administrator and try CONNECT again.",
                CloseButtonText = "OK",
                XamlRoot = xamlRoot
            };
            await restartDlg.ShowAsync();
            return false;
        }

        private static async Task<XamlRoot?> WaitForXamlRootAsync()
        {
            for (int i = 0; i < 10; i++)
            {
                XamlRoot? xamlRoot = TryResolveXamlRoot(null);
                if (xamlRoot != null) return xamlRoot;

                await Task.Delay(200);
            }

            return null;
        }

        private static XamlRoot? TryResolveXamlRoot(XamlRoot? preferred)
        {
            if (preferred != null) return preferred;

            try
            {
                return App.MainWindowInstance?.Content?.XamlRoot;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static async Task<bool> LaunchInstallerAsync(string installerPath, XamlRoot xamlRoot)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo(installerPath)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = Path.GetDirectoryName(installerPath) ?? AppContext.BaseDirectory
                });

                if (process != null)
                    await process.WaitForExitAsync();

                return true;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                var dlg = new ContentDialog
                {
                    Title = "Driver Installer Failed",
                    Content = $"WiitarThing could not start the SCP driver installer: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = xamlRoot
                };
                await dlg.ShowAsync();
                return false;
            }
        }

        private static string? FindInstallerPath()
        {
            foreach (string root in GetSearchRoots())
            {
                string path = Path.Combine(root, "Installers", "Drivers", "SCP_Driver", "ScpDriver.exe");
                if (File.Exists(path)) return path;

                path = Path.Combine(root, "Drivers", "SCP_Driver", "ScpDriver.exe");
                if (File.Exists(path)) return path;

                path = Path.Combine(root, "SCP_Driver", "ScpDriver.exe");
                if (File.Exists(path)) return path;
            }

            return null;
        }

        private static string[] GetSearchRoots()
        {
            var roots = new System.Collections.Generic.List<string>();
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null && roots.Count < 8)
            {
                roots.Add(current.FullName);
                current = current.Parent;
            }

            return roots.ToArray();
        }
    }
}
