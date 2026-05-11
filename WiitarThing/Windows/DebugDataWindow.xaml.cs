using System;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NintrollerLib;

namespace WiinUSoft.Windows
{
    public partial class DebugDataWindow : ContentDialog
    {
        public bool Cancelled { get; protected set; }
        public Nintroller? nintroller;

        public DebugDataWindow()
        {
            InitializeComponent();
            Closed += DebugDataWindow_Closed;
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
            Hide();
        }

        private void DebugDataWindow_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            if (!Cancelled) Cancelled = true;
            if (nintroller != null)
                nintroller.StateUpdate -= Nintroller_StateUpdate;
        }
    }
}
