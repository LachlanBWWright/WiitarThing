using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;

namespace WiinUSoft.Windows
{
    public partial class DefaultCalibrationWindow : ContentDialog
    {
        private bool _opened;
        private readonly List<Property> _copyPrefs = new List<Property>();

        public DefaultCalibrationWindow()
        {
            InitializeComponent();
        }

        private void ContentDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            if (_opened)
                return;

            _opened = true;

            if (UserPrefs.Instance.defaultProperty != null)
            {
                switch (UserPrefs.Instance.defaultProperty.calPref)
                {
                    case Property.CalibrationPreference.Minimal: radioMin.IsChecked = true; break;
                    case Property.CalibrationPreference.Default: radioDefault.IsChecked = true; break;
                    case Property.CalibrationPreference.More: radioMod.IsChecked = true; break;
                    case Property.CalibrationPreference.Extra: radioExt.IsChecked = true; break;
                }
            }

            foreach (var pref in UserPrefs.Instance.devicePrefs)
            {
                if (pref.hid != "all")
                {
                    copyCombo.Items.Add(pref.name);
                    _copyPrefs.Add(pref);
                }
            }

            if (copyCombo.Items.Count > 0)
            {
                radioCopy.IsEnabled = true;
                copyCombo.IsEnabled = true;
                copyCombo.SelectedIndex = 0;
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var prop = new Property
            {
                hid = "all",
                name = "Default"
            };

            if (radioDefault.IsChecked == true)
            {
                prop.calPref = Property.CalibrationPreference.Default;
            }
            else if (radioMin.IsChecked == true)
            {
                prop.calPref = Property.CalibrationPreference.Minimal;
            }
            else if (radioMod.IsChecked == true)
            {
                prop.calPref = Property.CalibrationPreference.More;
            }
            else if (radioExt.IsChecked == true)
            {
                prop.calPref = Property.CalibrationPreference.Extra;
            }
            else if (radioCopy.IsChecked == true && copyCombo.SelectedIndex >= 0)
            {
                prop.calPref = Property.CalibrationPreference.Custom;
                var copy = _copyPrefs[copyCombo.SelectedIndex];
                prop.autoConnect = copy.autoConnect;
                prop.autoNum = copy.autoNum;
                prop.calString = copy.calString;
                prop.rumbleIntensity = copy.rumbleIntensity;
                prop.useRumble = copy.useRumble;
            }

            UserPrefs.Instance.defaultProperty = prop;
            UserPrefs.SavePrefs();
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            UserPrefs.Instance.devicePrefs.Remove(UserPrefs.Instance.defaultProperty);
            UserPrefs.Instance.defaultProfile = null;
            UserPrefs.SavePrefs();
        }
    }
}
