using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WiinUSoft.ViewModels;

namespace WiinUSoft
{
    public partial class PropWindow : ContentDialog
    {
        public bool doSave        = false;
        public bool customCalibrate = false;
        public Property props;
        private readonly PropertiesViewModel _viewModel;

        PropWindow(Property org) : this(org, "Controller") { }

        public PropWindow(Property org, string defaultName)
        {
            InitializeComponent();
            props = new Property(org);
            _viewModel = new PropertiesViewModel(props, defaultName);
            DataContext = _viewModel;
        }

        private void cancelBtn_Click(object sender, RoutedEventArgs e) { customCalibrate = false; Hide(); }
        private void saveBtn_Click(object sender, RoutedEventArgs e)   { customCalibrate = false; doSave = true; Hide(); }

        private void defaultBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog
            {
                DefaultExt = ".wsp",
                Filter     = App.PROFILE_FILTER
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK && System.IO.File.Exists(dialog.FileName))
                defaultInput.Text = dialog.FileName;
        }

        private void Calibration_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (calibrationSelection.SelectedIndex != 4)
                customCalibrate = false;
        }

        private void calibrationSelection_DropDownClosed(object sender, object e)
        {
            if (props != null && calibrationSelection.SelectedIndex == 4)
            {
                _viewModel.CalibrationIndex = 4;
                customCalibrate = true;
                Hide();  // caller (DeviceControl) will show CalibrateWindow then re-open us
            }
        }

    }
}
