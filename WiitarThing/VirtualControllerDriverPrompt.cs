using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ScpControl;
using WiinUSoft.Holders;
using WiinUSoft.VirtualOutput;

namespace WiinUSoft
{
    internal static class VirtualControllerDriverPrompt
    {
        private const string HidMaestroDownloadUrl = "https://hidmaestro.org/";
        private const string VJoyDownloadUrl = "https://github.com/shauleiz/vJoy/releases";
        private static bool _startupPromptShown;
        private static readonly SemaphoreSlim DialogGate = new(1, 1);

        public static async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
        {
            await DialogGate.WaitAsync();
            try
            {
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    try
                    {
                        return await dialog.ShowAsync();
                    }
                    catch (COMException ex) when (ex.Message.Contains("Only a single ContentDialog can be open", StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine(ex);
                        await Task.Delay(200);
                    }
                }

                return ContentDialogResult.None;
            }
            finally
            {
                DialogGate.Release();
            }
        }

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

        public static async Task CheckVJoyAtStartupAsync()
        {
            if (_startupPromptShown || VJoyBackend.IsDriverAvailable()) return;

            XamlRoot? xamlRoot = await WaitForXamlRootAsync();
            if (xamlRoot == null) return;

            _startupPromptShown = true;
            await PromptVJoyInstallAsync(xamlRoot);
        }

        public static async Task CheckHidMaestroAtStartupAsync()
        {
            if (_startupPromptShown || HidMaestroBackend.IsRuntimeAvailable()) return;

            XamlRoot? xamlRoot = await WaitForXamlRootAsync();
            if (xamlRoot == null) return;

            _startupPromptShown = true;
            await PromptHidMaestroInstallAsync(xamlRoot);
        }

        public static async Task<bool> PromptInstallAsync(XamlRoot? xamlRoot, bool startupCheck = false)
        {
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

            ContentDialogResult result = await ShowDialogAsync(dlg);

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
            await ShowDialogAsync(restartDlg);
            return false;
        }

        public static async Task<bool> PromptVJoyInstallAsync(XamlRoot? xamlRoot)
        {
            xamlRoot = TryResolveXamlRoot(xamlRoot);
            if (xamlRoot == null) return false;

            string? dllPath = VJoyBackend.FindVJoyInterfacePath();
            if (dllPath != null)
                return VJoyBackend.IsDriverAvailable();

            string? installerPath = FindVJoyInstallerPath();
            string content = installerPath == null
                ? "WiitarThing could not find vJoyInterface.dll. Install vJoy, then restart WiitarThing. If vJoy is already installed, make sure vJoyInterface.dll is available in the vJoy installation folder or next to WiitarThing."
                : "WiitarThing could not find vJoyInterface.dll. Install vJoy, then restart WiitarThing before selecting the vJoy backend.";

            var dlg = new ContentDialog
            {
                Title = "vJoy Required",
                Content = content,
                PrimaryButtonText = installerPath == null ? "Open vJoy Download" : "Install vJoy",
                SecondaryButtonText = "Retry",
                CloseButtonText = "Cancel",
                XamlRoot = xamlRoot
            };

            ContentDialogResult result = await ShowDialogAsync(dlg);

            if (result == ContentDialogResult.Secondary)
                return VJoyBackend.IsDriverAvailable();

            if (result != ContentDialogResult.Primary)
                return false;

            if (installerPath != null)
                return await LaunchInstallerAsync(installerPath, xamlRoot);

            return await LaunchDownloadPageAsync(VJoyDownloadUrl, xamlRoot);
        }

        public static async Task<bool> PromptHidMaestroInstallAsync(XamlRoot? xamlRoot)
        {
            xamlRoot = TryResolveXamlRoot(xamlRoot);
            if (xamlRoot == null) return false;

            string? dllPath = HidMaestroBackend.FindHidMaestroPath();
            bool bundled = dllPath != null;
            if (bundled && HidMaestroBackend.IsRuntimeAvailable())
                return true;

            string content = bundled
                ? "HIDMaestro.Core.dll is bundled, but the HIDMaestro driver is not installed or is not ready. Restart WiitarThing as administrator and select the HIDMaestro backend again to install the embedded driver."
                : "WiitarThing could not find HIDMaestro.Core.dll. Bundle it under Drivers\\HIDMaestro or install HIDMaestro, then restart WiitarThing.";

            var dlg = new ContentDialog
            {
                Title = "HIDMaestro Required",
                Content = content,
                PrimaryButtonText = bundled ? "Retry" : "Open HIDMaestro Download",
                SecondaryButtonText = bundled ? "" : "Retry",
                CloseButtonText = "Cancel",
                XamlRoot = xamlRoot
            };

            ContentDialogResult result = await ShowDialogAsync(dlg);

            if (result == ContentDialogResult.Secondary || (bundled && result == ContentDialogResult.Primary))
                return HidMaestroBackend.IsRuntimeAvailable();

            if (result != ContentDialogResult.Primary)
                return false;

            return await LaunchDownloadPageAsync(HidMaestroDownloadUrl, xamlRoot);
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
                await ShowDialogAsync(dlg);
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

        private static string? FindVJoyInstallerPath()
        {
            foreach (string root in GetSearchRoots())
            {
                foreach (string fileName in new[] { "vJoySetup.exe", "vJoySetup64.exe", "vJoyInstall.exe" })
                {
                    string path = Path.Combine(root, "Installers", "Drivers", "vJoy", fileName);
                    if (File.Exists(path)) return path;

                    path = Path.Combine(root, "Drivers", "vJoy", fileName);
                    if (File.Exists(path)) return path;

                    path = Path.Combine(root, "vJoy", fileName);
                    if (File.Exists(path)) return path;
                }
            }

            return null;
        }

        private static async Task<bool> LaunchDownloadPageAsync(string url, XamlRoot xamlRoot)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url)
                {
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                var dlg = new ContentDialog
                {
                    Title = "Could Not Open Download Page",
                    Content = $"Open this page manually to install vJoy: {url}",
                    CloseButtonText = "OK",
                    XamlRoot = xamlRoot
                };
                await ShowDialogAsync(dlg);
                return false;
            }
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
