using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace WiinUSoft
{
    /// <summary>
    /// Manages a system-tray icon using Windows Forms NotifyIcon.
    /// Requires UseWindowsForms=true in the project file (already set).
    /// </summary>
    internal sealed class TrayIconService : IDisposable
    {
        public event EventHandler? ShowRequested;
        public event EventHandler? RefreshRequested;
        public event EventHandler? ExitRequested;

        private readonly NotifyIcon _notifyIcon;
        private bool _disposed;

        public TrayIconService()
        {
            _notifyIcon = new NotifyIcon { Text = "WiitarThing", Visible = false };

            string iconPath = Path.Combine(AppContext.BaseDirectory, "GHWT_Wii_Guitar.ico");
            if (File.Exists(iconPath))
                _notifyIcon.Icon = new Icon(iconPath);

            var menu = new ContextMenuStrip();

            var showItem = (ToolStripMenuItem)menu.Items.Add("Show");
            showItem.Font = new Font(showItem.Font, System.Drawing.FontStyle.Bold);
            showItem.Click += (s, e) => ShowRequested?.Invoke(this, EventArgs.Empty);

            menu.Items.Add("Refresh").Click += (s, e) => RefreshRequested?.Invoke(this, EventArgs.Empty);
            menu.Items.Add("Exit").Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);

            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.DoubleClick += (s, e) => ShowRequested?.Invoke(this, EventArgs.Empty);
        }

        public bool IsVisible => _notifyIcon.Visible;
        public void Show() => _notifyIcon.Visible = true;
        public void Hide() => _notifyIcon.Visible = false;

        /// <param name="icon">0=None, 1=Info, 2=Warning, 3=Error</param>
        public void ShowBalloon(string title, string message, int icon = 0)
        {
            _notifyIcon.Visible = true;
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.BalloonTipIcon = icon switch
            {
                1 => ToolTipIcon.Info,
                2 => ToolTipIcon.Warning,
                3 => ToolTipIcon.Error,
                _ => ToolTipIcon.None,
            };
            _notifyIcon.ShowBalloonTip(7000);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _disposed = true;
            }
        }
    }
}
