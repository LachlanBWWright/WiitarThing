using Microsoft.UI.Xaml;

namespace WiinUSoft.Windows
{
    public partial class RemoveAllWiimotesWindow : Window
    {
        private System.Threading.Thread? _workThread;

        public RemoveAllWiimotesWindow()
        {
            InitializeComponent();
            Activated += Window_Activated;
        }

        private bool _started;
        private void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (_started) return;
            _started = true;
            Activated -= Window_Activated;

            _workThread = new System.Threading.Thread(() =>
            {
                SyncDialog.RemoveAllWiimotes();
                DispatcherQueue.TryEnqueue(Close);
            });
            _workThread.IsBackground = true;
            _workThread.Start();
        }
    }
}
