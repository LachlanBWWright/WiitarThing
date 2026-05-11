using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Shared;

namespace WiinUSoft
{
    public partial class ErrorWindow : ContentDialog
    {
        public ErrorWindow() { InitializeComponent(); }

        public ErrorWindow(Exception ex) : this()
        {
            _errorMessage.Text = ex.Message;
            _errorStack.Text   = ex.StackTrace;

            if (ex.Message.Contains("NintrollerLib"))
            {
                var versionResult = TryLoadNintrollerVersion("Nintroller.dll");
                if (versionResult.IsError)
                {
                    System.Diagnostics.Debug.WriteLine(versionResult.Error.ToString());
                }

                if (versionResult.IsOk && versionResult.Value < new Version(2, 5))
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
        }

        private static Result<Version, PreferencesError> TryLoadNintrollerVersion(string assemblyPath)
        {
            try
            {
                if (!File.Exists(assemblyPath))
                    return Result<Version, PreferencesError>.Err(PreferencesError.FileNotFound(assemblyPath));

                var version = System.Reflection.AssemblyName.GetAssemblyName(assemblyPath).Version;
                if (version == null)
                    return Result<Version, PreferencesError>.Err(
                        PreferencesError.ValidationFailed("Assembly version was not found.", assemblyPath));

                return Result<Version, PreferencesError>.Ok(version);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Result<Version, PreferencesError>.Err(PreferencesError.AccessDenied(assemblyPath, ex));
            }
            catch (IOException ex)
            {
                return Result<Version, PreferencesError>.Err(PreferencesError.Unknown(assemblyPath, ex));
            }
            catch (BadImageFormatException ex)
            {
                return Result<Version, PreferencesError>.Err(
                    PreferencesError.ValidationFailed($"Assembly '{assemblyPath}' is not a valid .NET assembly: {ex.Message}", assemblyPath));
            }
        }

        public System.Threading.Tasks.Task<ContentDialogResult> ShowDialogAsync()
            => this.ShowAsync().AsTask();

        private void _dontSendBtn_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            Application.Current.Exit();
        }
    }
}
