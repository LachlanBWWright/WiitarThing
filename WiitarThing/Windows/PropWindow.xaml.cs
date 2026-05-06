using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WiinUSoft
{
    public partial class PropWindow : Window
    {
        public bool doSave        = false;
        public bool customCalibrate = false;
        public Property props;

        PropWindow(Property org) : this(org, "Controller") { }

        public PropWindow(Property org, string defaultName)
        {
            InitializeComponent();
            props = new Property(org);
            nameInput.Text = string.IsNullOrWhiteSpace(props.name) ? defaultName : props.name;
            defaultInput.Text = props.profile;
            autoCheckbox.IsChecked = props.autoConnect;
            if (props.autoNum >= 0 && props.autoNum <= autoConnectNumber.Items.Count)
                autoConnectNumber.SelectedIndex = props.autoNum;
            if (props.rumbleIntensity >= 0 && props.rumbleIntensity <= rumbleSelection.Items.Count)
                rumbleSelection.SelectedIndex = props.rumbleIntensity;
            calibrationSelection.SelectedIndex = props.calPref switch
            {
                Property.CalibrationPreference.Minimal => 1,
                Property.CalibrationPreference.More    => 2,
                Property.CalibrationPreference.Extra   => 3,
                Property.CalibrationPreference.Custom  => 4,
                _                                      => 0
            };
            pointerSelection.SelectedIndex = (int)org.pointerMode;
        }

        private void cancelBtn_Click(object sender, RoutedEventArgs e) { customCalibrate = false; Close(); }
        private void saveBtn_Click(object sender, RoutedEventArgs e)   { customCalibrate = false; doSave = true; Close(); }

        private void autoCheckbox_Click(object sender, RoutedEventArgs e)
            => props.autoConnect = autoCheckbox.IsChecked == true;

        private void nameInput_TextChanged(object sender, TextChangedEventArgs e)
            => props.name = nameInput.Text;

        private void defaultInput_TextChanged(object sender, TextChangedEventArgs e)
            => props.profile = defaultInput.Text;

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
            }
        }

        private void Rumble_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (props != null)
            {
                props.useRumble       = rumbleSelection.SelectedIndex > 0;
                props.rumbleIntensity = rumbleSelection.SelectedIndex;
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
                if (calibrationSelection.SelectedIndex != 4) customCalibrate = false;
            }
        }

        private void calibrationSelection_DropDownClosed(object sender, object e)
        {
            if (props != null && calibrationSelection.SelectedIndex == 4)
            {
                props.calPref   = Property.CalibrationPreference.Custom;
                customCalibrate = true;
                Close();  // caller (DeviceControl) will show CalibrateWindow then re-open us
            }
        }

        private void pointerSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (props != null)
                props.pointerMode = (Property.PointerOffScreenMode)pointerSelection.SelectedIndex;
        }
    }
}
