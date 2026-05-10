using System;
using System.Text;
using Microsoft.UI.Xaml;
using NintrollerLib;

namespace WiinUSoft.Windows
{
    public partial class DebugDataWindow : Window
    {
        public bool Cancelled { get; protected set; }
        public Nintroller? nintroller;

        public DebugDataWindow()
        {
            InitializeComponent();
            AppWindow.Closing += AppWindow_Closing;
        }

        public void RegisterNintrollerUpdate()
        {
            if (nintroller != null)
                nintroller.StateUpdate += Nintroller_StateUpdate;
        }

        private void Nintroller_StateUpdate(object? sender, NintrollerStateEventArgs e)
        {
#if DEBUG
            if (!Cancelled && e.state is WiiGuitar wgt)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < wgt.DebugLastData.Length; i++)
                    sb.Append(wgt.DebugLastData[i].ToString("X2") + " ");
                Prompt(sb.ToString());
            }
#endif
        }

        private void Prompt(string text, bool isBold = false, bool isItalic = false, bool isSmall = false, bool isDebug = false)
        {
            WiitarDebug.Log("SYNC WINDOW OUTPUT: \n\n" + text + "\n\n");
            DispatcherQueue.TryEnqueue(() =>
            {
                promptBox.Text            = text + "\n";
                promptBox.SelectionStart  = promptBox.Text.Length;
                promptBox.SelectionLength = 0;
            });
        }

        private void cancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Prompt("Stopping...");
            Cancelled = true;
        }

        private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            if (!Cancelled)
            {
                Cancelled = true;
                Prompt("Stopping...");
                args.Cancel = true;
                return;
            }
            if (nintroller != null)
                nintroller.StateUpdate -= Nintroller_StateUpdate;
        }
    }
}
