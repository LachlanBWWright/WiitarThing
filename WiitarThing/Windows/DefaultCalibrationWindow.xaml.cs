using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using WiinUSoft.ViewModels;

namespace WiinUSoft.Windows
{
    public partial class DefaultCalibrationWindow : ContentDialog
    {
        private bool _opened;
        private readonly DefaultCalibrationViewModel _viewModel = new();

        public DefaultCalibrationWindow()
        {
            InitializeComponent();
            DataContext = _viewModel;
        }

        private void ContentDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            if (_opened)
                return;

            _opened = true;

            _viewModel.LoadCurrent();
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var saveResult = _viewModel.Save();
            if (saveResult.IsError)
                Debug.WriteLine(saveResult.Error.ToDisplayString());
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var saveResult = _viewModel.Clear();
            if (saveResult.IsError)
                Debug.WriteLine(saveResult.Error.ToDisplayString());
        }
    }
}
