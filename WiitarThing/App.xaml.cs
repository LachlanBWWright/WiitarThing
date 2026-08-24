using Microsoft.Shell;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;

namespace WiinUSoft
{
    /// <summary>
    /// WinUI 3 application entry point.
    /// </summary>
    public partial class App : Application, ISingleInstanceApp
    {
        internal const string PROFILE_FILTER = "WiinUSoft Profile|*.wsp";
        private const string Unique = "wiinupro-or-wiinusoft-instance";

        private static MainWindow? _mainWindow;
        private static bool _captureUiGallery;
        internal static MainWindow? MainWindowInstance => _mainWindow;

        [STAThread]
        public static void Main(string[] args)
        {
            foreach (string arg in args)
            {
                if (string.Equals(arg, "--capture-ui-gallery", StringComparison.OrdinalIgnoreCase))
                {
                    _captureUiGallery = true;
                    break;
                }
            }

            EarlyLog("Main begin");
            System.Threading.Thread.Sleep(250);

            if (SingleInstance<App>.InitializeAsFirstInstance(Unique))
            {
                EarlyLog("First instance initialized");
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                WinRT.ComWrappersSupport.InitializeComWrappers();
                Application.Start(p =>
                {
                    EarlyLog("Application.Start callback begin");
                    var ctx = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                    System.Threading.SynchronizationContext.SetSynchronizationContext(ctx);
                    _ = new App();
                    EarlyLog("Application.Start callback end");
                });

                SingleInstance<App>.Cleanup();
                EarlyLog("Main end");
            }
            else
            {
                EarlyLog("Second instance signaled first instance");
            }
        }

        static void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception)args.ExceptionObject;
            WiitarDebug.Log($"ERROR:\n{e}", WiitarDebug.LogLevel.Error);

            // Show on UI thread if possible
            var mainWindow = _mainWindow;
            if (mainWindow != null && mainWindow.DispatcherQueue != null)
            {
                mainWindow.DispatcherQueue.TryEnqueue(async () =>
                {
                    var box = new ErrorWindow(e);
                    var xamlRoot = mainWindow.Content?.XamlRoot;
                    if (xamlRoot != null)
                    {
                        box.XamlRoot = xamlRoot;
                    }
                    else
                    {
                        Debug.WriteLine("Skipping error dialog XamlRoot assignment because MainWindow.Content is unavailable.");
                    }
                    await box.ShowDialogAsync();
                    SingleInstance<App>.Cleanup();
                    Application.Current.Exit();
                });
            }
            else
            {
                Debug.WriteLine("Skipping error dialog because MainWindow or DispatcherQueue is unavailable.");
                SingleInstance<App>.Cleanup();
            }
        }

        public App()
        {
            EarlyLog("App ctor begin");
            UnhandledException += App_UnhandledException;
            InitializeComponent();
            EarlyLog("App ctor end");
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            EarlyLog("OnLaunched begin");

            if (_captureUiGallery)
            {
                _ = CaptureUiGalleryAndExitAsync();
                return;
            }

            // Load controller icon BitmapImages from the output directory
            LoadIconResources();
            EarlyLog("Icons loaded");

            _mainWindow = new MainWindow();
            EarlyLog("MainWindow constructed");
            _mainWindow.Activate();
            EarlyLog("MainWindow activated");
        }

        private static async Task CaptureUiGalleryAndExitAsync()
        {
            try
            {
                var gallery = new Windows.DesignGalleryWindow();
                gallery.Activate();
                await Task.Delay(500);
                await gallery.CaptureAllAsync();
                gallery.Close();
                SingleInstance<App>.Cleanup();
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                EarlyLog($"UI gallery capture failed: {ex}");
                SingleInstance<App>.Cleanup();
                Environment.Exit(1);
            }
        }

        private static void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            EarlyLog($"WinUI unhandled exception:\n{e.Exception}");
            WiitarDebug.Log($"ERROR:\n{e.Exception}", WiitarDebug.LogLevel.Error);
        }

        internal static void EarlyLog(string message)
        {
            try
            {
                File.AppendAllText(
                    Path.Combine(AppContext.BaseDirectory, "WiitarStartup.log"),
                    $"[{DateTime.Now:O}] {message}{Environment.NewLine}");
            }
            catch (UnauthorizedAccessException)
            {
                // Logging failures should not crash startup.
            }
            catch (IOException)
            {
                // Logging failures should not crash startup.
            }
            catch (System.Security.SecurityException)
            {
                // Logging failures should not crash startup.
            }
        }

        /// <summary>
        /// Loads controller icon images into Application.Resources from the exe directory.
        /// </summary>
        private void LoadIconResources()
        {
            var baseDir = AppContext.BaseDirectory;
            var iconResources = new ResourceDictionary();
            void Add(string key, string relPath)
            {
                var fullPath = Path.Combine(baseDir, relPath);
                if (File.Exists(fullPath))
                    iconResources[key] = new BitmapImage(new Uri(fullPath));
            }

            Add("ProIcon", Path.Combine("Images", "ProController_white_32.png"));
            Add("CCIcon", Path.Combine("Images", "Classic_white_32.png"));
            Add("CCPIcon", Path.Combine("Images", "ClassicPro_white_32.png"));
            Add("WIcon", Path.Combine("Images", "wiimote_white_32.png"));
            Add("WNIcon", Path.Combine("Images", "WiimoteNunchuk_white_32.png"));
            Add("UIcon", Path.Combine("Images", "unknown.png"));
            Add("WGTIcon", Path.Combine("Images", "GHWT_Wii_Guitar.png"));
            Add("WDRIcon", Path.Combine("Images", "GHWT_Wii_Drums.png"));

            Resources.MergedDictionaries.Add(iconResources);
        }

        public bool SignalExternalCommandLineArgs(IList<string> args)
        {
            // Show the original instance
            var mainWindow = _mainWindow;
            if (mainWindow == null || mainWindow.DispatcherQueue == null)
            {
                Debug.WriteLine("Unable to signal the main window because it is not available.");
                return true;
            }

            mainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                mainWindow.ShowWindow();

                // Show "already running" info dialog
                mainWindow.DispatcherQueue.TryEnqueue(async () =>
                {
                    var xamlRoot = mainWindow.Content?.XamlRoot;
                    if (xamlRoot == null)
                    {
                        Debug.WriteLine("Skipping already-running dialog because MainWindow.Content is unavailable.");
                        return;
                    }

                    var dlg = new Microsoft.UI.Xaml.Controls.ContentDialog
                    {
                        Title = "WiitarThing Already Running",
                        Content = "WiitarThing was already running so the previous instance was brought into focus.",
                        CloseButtonText = "OK",
                        XamlRoot = xamlRoot
                    };
                    await dlg.ShowAsync();
                });
            });

            return true;
        }
    }
}
