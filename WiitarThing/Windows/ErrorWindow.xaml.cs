using System;
using Microsoft.UI.Xaml;

namespace WiinUSoft
{
    public partial class ErrorWindow : Window
    {
        public ErrorWindow() { InitializeComponent(); }

        public ErrorWindow(Exception ex) : this()
        {
            _errorMessage.Text = ex.Message;
            _errorStack.Text   = ex.StackTrace;

            if (ex.Message.Contains("NintrollerLib"))
            {
                try
                {
                    var nVersion = System.Reflection.Assembly.LoadFrom("Nintroller.dll").GetName().Version;
                    if (nVersion < new Version(2, 5))
                    {
                        _errorMessage.Text = "The Nintroller library is out of date.";
                        _errorStack.Text   = "Please try the following:" + Environment.NewLine +
                            Environment.NewLine + "1) Uninstall WiinUSoft" +
                            Environment.NewLine + "2) Reinstall WiinUSoft using the latest installer" +
                            Environment.NewLine + "3) Verify that the installed Nintroller.dll in the installation folder" +
                            " is version 2.5 by right clicking the file, choosing Properties, and choose the Details tab.";
                        _dontSendBtn.Content = "Close";
                    }
                }
                catch { }
            }
        }

        public System.Threading.Tasks.Task ShowDialogAsync()
            => this.ShowAsDialogAsync();

        private void _dontSendBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
            Application.Current.Exit();
        }
    }
}
