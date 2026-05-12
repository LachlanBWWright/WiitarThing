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
            nameInput.Text = _viewModel.Name;
            defaultInput.Text = _viewModel.ProfilePath;
            autoCheckbox.IsChecked = props.autoConnect;
            if (props.autoNum >= 0 && props.autoNum <= autoConnectNumber.Items.Count)
                autoConnectNumber.SelectedIndex = props.autoNum;
            if (props.rumbleIntensity >= 0 && props.rumbleIntensity <= rumbleSelection.Items.Count)
                rumbleSelection.SelectedIndex = props.rumbleIntensity;
            calibrationSelection.SelectedIndex = _viewModel.CalibrationIndex;
            pointerSelection.SelectedIndex = _viewModel.PointerModeIndex;
        }

        private void cancelBtn_Click(object sender, RoutedEventArgs e) { customCalibrate = false; Hide(); }
        private void saveBtn_Click(object sender, RoutedEventArgs e)   { customCalibrate = false; doSave = true; Hide(); }

        private void autoCheckbox_Click(object sender, RoutedEventArgs e)
            => props.autoConnect = autoCheckbox.IsChecked == true;

        private void nameInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            _viewModel.Name = nameInput.Text;
            props.name = _viewModel.Name;
        }

        private void defaultInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            _viewModel.ProfilePath = defaultInput.Text;
            props.profile = _viewModel.ProfilePath;
        }

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

        private void AutoConnect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (props != null)
            {
                props.autoConnect = autoConnectNumber.SelectedIndex > 0;
                props.autoNum     = autoConnectNumber.SelectedIndex;
                _viewModel.AutoConnectIndex = autoConnectNumber.SelectedIndex;
            }
        }

        private void Rumble_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (props != null)
            {
                props.useRumble       = rumbleSelection.SelectedIndex > 0;
                props.rumbleIntensity = rumbleSelection.SelectedIndex;
                _viewModel.RumbleIndex = rumbleSelection.SelectedIndex;
            }
        }

        private void Calibration_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (props != null)
            {
                props.calPref = calibrationSelection.SelectedIndex switch
                {
                    1 => Property.CalibrationPreference.Minimal,
                    2 => Property.CalibrationPreference.More,
                    3 => Property.CalibrationPreference.Extra,
                    _ => Property.CalibrationPreference.Default
                };
                _viewModel.CalibrationIndex = calibrationSelection.SelectedIndex;
                if (calibrationSelection.SelectedIndex != 4) customCalibrate = false;
            }
        }

        private void calibrationSelection_DropDownClosed(object sender, object e)
        {
            if (props != null && calibrationSelection.SelectedIndex == 4)
            {
                props.calPref   = Property.CalibrationPreference.Custom;
                customCalibrate = true;
                Hide();  // caller (DeviceControl) will show CalibrateWindow then re-open us
            }
        }

        private void pointerSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (props != null)
            {
                props.pointerMode = (Property.PointerOffScreenMode)pointerSelection.SelectedIndex;
                _viewModel.PointerModeIndex = pointerSelection.SelectedIndex;
            }
        }
    }
}
