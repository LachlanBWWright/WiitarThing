using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WiinUSoft.Windows
{
    public partial class RemoveAllWiimotesWindow : ContentDialog
    {
        private System.Threading.Thread? _workThread;

        public RemoveAllWiimotesWindow()
        {
            InitializeComponent();
            Loaded += Window_Loaded;
        }

        private bool _started;
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_started) return;
            _started = true;
            Loaded -= Window_Loaded;

            _workThread = new System.Threading.Thread(() =>
            {
                SyncDialog.RemoveAllWiimotes();
                DispatcherQueue.TryEnqueue(Hide);
            });
            _workThread.IsBackground = true;
            _workThread.Start();
        }
    }
}
