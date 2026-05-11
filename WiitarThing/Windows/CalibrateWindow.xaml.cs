using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NintrollerLib;

namespace WiinUSoft
{
    public partial class CalibrateWindow : ContentDialog
    {
        enum CalibrationStep
        {
            Done, ChangeController,
            Wiimote_acc_x_center, Wiimote_acc_x_range,
            Wiimote_acc_y_center, Wiimote_acc_y_range,
            Wiimote_acc_z_center, Wiimote_acc_z_range,
            Nunchuk_acc_x_center, Nunchuk_acc_x_range,
            Nunchuk_acc_y_center, Nunchuk_acc_y_range,
            Nunchuk_acc_z_center, Nunchuk_acc_z_range,
            Nunchuk_acc_done, Nunchuk_joy_center, Nunchuk_joy_range, Nunchuk_joy_deadzone,
            Classic_joy_center, Classic_joy_range, Classic_joy_deadzone,
            ClassicPro_joy_center, ClassicPro_joy_range, ClassicPro_joy_deadzone,
            Pro_joy_center, Pro_joy_range, Pro_joy_deadzone
        }

        public bool doSave = false;
        public CalibrationStorage Calibration => _calibrations;

        private Nintroller _device = null!;
        private bool _changingType;
        private ControllerType _calibrationToSave = ControllerType.Unknown;
        private List<ControllerType> _calibratedTypes = new List<ControllerType>();
        private CalibrationStep _step = CalibrationStep.ChangeController;
        private CalibrationStorage _calibrations = new CalibrationStorage();

        private CalibrateWindow()
        {
            InitializeComponent();
            Closed += Dialog_Closed;
        }

        public CalibrateWindow(Nintroller device) : this()
        {
            _calibrations.SetCalibrations(device.StoredCalibrations.ToString());
            SelectStep(device.Type);
            _device = device;
            _device.StateUpdate += _device_StateUpdate;
            _device.ExtensionChange += _device_ExtensionChange;
        }

        private void Dialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            _device.StateUpdate -= _device_StateUpdate;
            _device.ExtensionChange -= _device_ExtensionChange;
        }

        private void SelectStep(ControllerType deviceType)
        {
            if (_calibratedTypes.Contains(deviceType)) return;
            _changingType = true;
            if (deviceType == ControllerType.ProController)
            {
                _step = CalibrationStep.Pro_joy_center;
            }
            else
            {
                StoreCalibration(_calibrationToSave);
                _step = deviceType switch
                {
                    ControllerType.Wiimote => CalibrationStep.Wiimote_acc_x_center,
                    ControllerType.Nunchuk => CalibrationStep.Nunchuk_acc_x_center,
                    ControllerType.NunchukB => CalibrationStep.Nunchuk_acc_x_center,
                    ControllerType.ClassicController => CalibrationStep.Classic_joy_center,
                    ControllerType.ClassicControllerPro => CalibrationStep.ClassicPro_joy_center,
                    ControllerType.ProController => CalibrationStep.Pro_joy_center,
                    _ => _step
                };
            }
            UpdateUI();
            _changingType = false;
        }

        void _device_ExtensionChange(object? sender, NintrollerExtensionEventArgs e)
            => DispatcherQueue.TryEnqueue(() => SelectStep(e.controllerType));

        void _device_StateUpdate(object? sender, NintrollerStateEventArgs e)
        {
            if (_changingType) return;
            DispatcherQueue.TryEnqueue(() =>
            {
                if (!CanHandleStateForStep(_step, e.state))
                {
                    System.Diagnostics.Debug.WriteLine($"Calibration sample ignored due to state type mismatch for step {_step}: {e.state.GetType().Name}");
                    return;
                }

                switch (_step)
                {
                    #region Wiimote
                    case CalibrationStep.Wiimote_acc_x_center: group1_center.Value = ((Wiimote)e.state).accelerometer.rawX; break;
                    case CalibrationStep.Wiimote_acc_x_range:
                        if (group1_max.Value == 0) { group1_min.Value = group1_max.Value = ((Wiimote)e.state).accelerometer.rawX; }
                        else { if (group1_min.Value > ((Wiimote)e.state).accelerometer.rawX) group1_min.Value = ((Wiimote)e.state).accelerometer.rawX; if (group1_max.Value < ((Wiimote)e.state).accelerometer.rawX) group1_max.Value = ((Wiimote)e.state).accelerometer.rawX; }
                        break;
                    case CalibrationStep.Wiimote_acc_y_center: group2_center.Value = ((Wiimote)e.state).accelerometer.rawY; break;
                    case CalibrationStep.Wiimote_acc_y_range:
                        if (group2_max.Value == 0) { group2_min.Value = group2_max.Value = ((Wiimote)e.state).accelerometer.rawY; }
                        else { if (group2_min.Value > ((Wiimote)e.state).accelerometer.rawY) group2_min.Value = ((Wiimote)e.state).accelerometer.rawY; if (group2_max.Value < ((Wiimote)e.state).accelerometer.rawY) group2_max.Value = ((Wiimote)e.state).accelerometer.rawY; }
                        break;
                    case CalibrationStep.Wiimote_acc_z_center: group3_center.Value = ((Wiimote)e.state).accelerometer.rawZ; break;
                    case CalibrationStep.Wiimote_acc_z_range:
                        if (group3_max.Value == 0) { group3_min.Value = group3_max.Value = ((Wiimote)e.state).accelerometer.rawZ; }
                        else { if (group3_min.Value > ((Wiimote)e.state).accelerometer.rawZ) group3_min.Value = ((Wiimote)e.state).accelerometer.rawZ; if (group3_max.Value < ((Wiimote)e.state).accelerometer.rawZ) group3_max.Value = ((Wiimote)e.state).accelerometer.rawZ; }
                        break;
                    #endregion
                    #region Nunchuk
                    case CalibrationStep.Nunchuk_acc_x_center: group1_center.Value = ((Nunchuk)e.state).accelerometer.rawX; break;
                    case CalibrationStep.Nunchuk_acc_x_range:
                        if (group1_max.Value == 0) { group1_min.Value = group1_max.Value = ((Nunchuk)e.state).accelerometer.rawX; }
                        else { if (group1_min.Value > ((Nunchuk)e.state).accelerometer.rawX) group1_min.Value = ((Nunchuk)e.state).accelerometer.rawX; if (group1_max.Value < ((Nunchuk)e.state).accelerometer.rawX) group1_max.Value = ((Nunchuk)e.state).accelerometer.rawX; }
                        break;
                    case CalibrationStep.Nunchuk_acc_y_center: group2_center.Value = ((Nunchuk)e.state).accelerometer.rawY; break;
                    case CalibrationStep.Nunchuk_acc_y_range:
                        if (group2_max.Value == 0) { group2_min.Value = group2_max.Value = ((Nunchuk)e.state).accelerometer.rawY; }
                        else { if (group2_min.Value > ((Nunchuk)e.state).accelerometer.rawY) group2_min.Value = ((Nunchuk)e.state).accelerometer.rawY; if (group2_max.Value < ((Nunchuk)e.state).accelerometer.rawY) group2_max.Value = ((Nunchuk)e.state).accelerometer.rawY; }
                        break;
                    case CalibrationStep.Nunchuk_acc_z_center: group3_center.Value = ((Nunchuk)e.state).accelerometer.rawZ; break;
                    case CalibrationStep.Nunchuk_acc_z_range:
                        if (group3_max.Value == 0) { group3_min.Value = group3_max.Value = ((Nunchuk)e.state).accelerometer.rawZ; }
                        else { if (group3_min.Value > ((Nunchuk)e.state).accelerometer.rawZ) group3_min.Value = ((Nunchuk)e.state).accelerometer.rawZ; if (group3_max.Value < ((Nunchuk)e.state).accelerometer.rawZ) group3_max.Value = ((Nunchuk)e.state).accelerometer.rawZ; }
                        break;
                    case CalibrationStep.Nunchuk_acc_done: break;
                    case CalibrationStep.Nunchuk_joy_center:
                        group1_center.Value = ((Nunchuk)e.state).joystick.rawX;
                        group2_center.Value = ((Nunchuk)e.state).joystick.rawY;
                        break;
                    case CalibrationStep.Nunchuk_joy_range:
                        if (group1_min.Value == 0) { group1_min.Value = ((Nunchuk)e.state).joystick.rawX; group1_max.Value = ((Nunchuk)e.state).joystick.rawX; group2_min.Value = ((Nunchuk)e.state).joystick.rawY; group2_max.Value = ((Nunchuk)e.state).joystick.rawY; }
                        else { if (group1_min.Value - 2 > ((Nunchuk)e.state).joystick.rawX) group1_min.Value = ((Nunchuk)e.state).joystick.rawX; if (group1_max.Value + 2 < ((Nunchuk)e.state).joystick.rawX) group1_max.Value = ((Nunchuk)e.state).joystick.rawX; if (group2_min.Value - 2 > ((Nunchuk)e.state).joystick.rawY) group2_min.Value = ((Nunchuk)e.state).joystick.rawY; if (group2_max.Value + 2 < ((Nunchuk)e.state).joystick.rawY) group2_max.Value = ((Nunchuk)e.state).joystick.rawY; }
                        break;
                    case CalibrationStep.Nunchuk_joy_deadzone:
                        { int nx = Math.Abs(((Nunchuk)e.state).joystick.rawX - group1_center.Value); int ny = Math.Abs(((Nunchuk)e.state).joystick.rawY - group2_center.Value); if (nx > group1_dead.Value) group1_dead.Value = nx; if (ny > group2_dead.Value) group2_dead.Value = ny; }
                        break;
                    #endregion
                    #region Classic Controller
                    case CalibrationStep.Classic_joy_center:
                        group1_center.Value = ((ClassicController)e.state).LJoy.rawX; group2_center.Value = ((ClassicController)e.state).LJoy.rawY;
                        group3_center.Value = ((ClassicController)e.state).RJoy.rawX; group4_center.Value = ((ClassicController)e.state).RJoy.rawY;
                        groupL_min.Value = ((ClassicController)e.state).L.rawValue; groupR_min.Value = ((ClassicController)e.state).R.rawValue;
                        break;
                    case CalibrationStep.Classic_joy_range:
                        if (group1_max.Value == 0) { group1_min.Value = ((ClassicController)e.state).LJoy.rawX; group1_max.Value = ((ClassicController)e.state).LJoy.rawX; group2_min.Value = ((ClassicController)e.state).LJoy.rawY; group2_max.Value = ((ClassicController)e.state).LJoy.rawY; group3_min.Value = ((ClassicController)e.state).RJoy.rawX; group3_max.Value = ((ClassicController)e.state).RJoy.rawX; group4_min.Value = ((ClassicController)e.state).RJoy.rawY; group4_max.Value = ((ClassicController)e.state).RJoy.rawY; groupL_max.Value = ((ClassicController)e.state).L.rawValue; groupR_max.Value = ((ClassicController)e.state).R.rawValue; }
                        else { if (group1_min.Value - 2 > ((ClassicController)e.state).LJoy.rawX) group1_min.Value = ((ClassicController)e.state).LJoy.rawX; if (group1_max.Value + 2 < ((ClassicController)e.state).LJoy.rawX) group1_max.Value = ((ClassicController)e.state).LJoy.rawX; if (group2_min.Value - 2 > ((ClassicController)e.state).LJoy.rawY) group2_min.Value = ((ClassicController)e.state).LJoy.rawY; if (group2_max.Value + 2 < ((ClassicController)e.state).LJoy.rawY) group2_max.Value = ((ClassicController)e.state).LJoy.rawY; if (group3_min.Value - 1 > ((ClassicController)e.state).RJoy.rawX) group3_min.Value = ((ClassicController)e.state).RJoy.rawX; if (group3_max.Value + 1 < ((ClassicController)e.state).RJoy.rawX) group3_max.Value = ((ClassicController)e.state).RJoy.rawX; if (group4_min.Value - 1 > ((ClassicController)e.state).RJoy.rawY) group4_min.Value = ((ClassicController)e.state).RJoy.rawY; if (group4_max.Value + 1 < ((ClassicController)e.state).RJoy.rawY) group4_max.Value = ((ClassicController)e.state).RJoy.rawY; if (groupL_max.Value + 1 < ((ClassicController)e.state).L.rawValue) groupL_max.Value = ((ClassicController)e.state).L.rawValue; if (groupR_max.Value - 1 < ((ClassicController)e.state).R.rawValue) groupR_max.Value = ((ClassicController)e.state).R.rawValue; }
                        break;
                    case CalibrationStep.Classic_joy_deadzone:
                        { int lx = Math.Abs(((ClassicController)e.state).LJoy.rawX - group1_center.Value); int ly = Math.Abs(((ClassicController)e.state).LJoy.rawY - group2_center.Value); int rx = Math.Abs(((ClassicController)e.state).RJoy.rawX - group3_center.Value); int ry = Math.Abs(((ClassicController)e.state).RJoy.rawY - group4_center.Value); if (lx > group1_dead.Value) group1_dead.Value = lx; if (ly > group2_dead.Value) group2_dead.Value = ly; if (rx > group3_dead.Value) group3_dead.Value = rx; if (ry > group4_dead.Value) group4_dead.Value = ry; }
                        break;
                    #endregion
                    #region CCPro
                    case CalibrationStep.ClassicPro_joy_center:
                        group1_center.Value = ((ClassicControllerPro)e.state).LJoy.rawX; group2_center.Value = ((ClassicControllerPro)e.state).LJoy.rawY;
                        group3_center.Value = ((ClassicControllerPro)e.state).RJoy.rawX; group4_center.Value = ((ClassicControllerPro)e.state).RJoy.rawY;
                        break;
                    case CalibrationStep.ClassicPro_joy_range:
                        if (group1_max.Value == 0) { group1_min.Value = ((ClassicControllerPro)e.state).LJoy.rawX; group1_max.Value = ((ClassicControllerPro)e.state).LJoy.rawX; group2_min.Value = ((ClassicControllerPro)e.state).LJoy.rawY; group2_max.Value = ((ClassicControllerPro)e.state).LJoy.rawY; group3_min.Value = ((ClassicControllerPro)e.state).RJoy.rawX; group3_max.Value = ((ClassicControllerPro)e.state).RJoy.rawX; group4_min.Value = ((ClassicControllerPro)e.state).RJoy.rawY; group4_max.Value = ((ClassicControllerPro)e.state).RJoy.rawY; }
                        else { if (group1_min.Value - 2 > ((ClassicControllerPro)e.state).LJoy.rawX) group1_min.Value = ((ClassicControllerPro)e.state).LJoy.rawX; if (group1_max.Value + 2 < ((ClassicControllerPro)e.state).LJoy.rawX) group1_max.Value = ((ClassicControllerPro)e.state).LJoy.rawX; if (group2_min.Value - 2 > ((ClassicControllerPro)e.state).LJoy.rawY) group2_min.Value = ((ClassicControllerPro)e.state).LJoy.rawY; if (group2_max.Value + 2 < ((ClassicControllerPro)e.state).LJoy.rawY) group2_max.Value = ((ClassicControllerPro)e.state).LJoy.rawY; if (group3_min.Value - 1 > ((ClassicControllerPro)e.state).RJoy.rawX) group3_min.Value = ((ClassicControllerPro)e.state).RJoy.rawX; if (group3_max.Value + 1 < ((ClassicControllerPro)e.state).RJoy.rawX) group3_max.Value = ((ClassicControllerPro)e.state).RJoy.rawX; if (group4_min.Value - 1 > ((ClassicControllerPro)e.state).RJoy.rawY) group4_min.Value = ((ClassicControllerPro)e.state).RJoy.rawY; if (group4_max.Value + 1 < ((ClassicControllerPro)e.state).RJoy.rawY) group4_max.Value = ((ClassicControllerPro)e.state).RJoy.rawY; }
                        break;
                    case CalibrationStep.ClassicPro_joy_deadzone:
                        { int lx = Math.Abs(((ClassicControllerPro)e.state).LJoy.rawX - group1_center.Value); int ly = Math.Abs(((ClassicControllerPro)e.state).LJoy.rawY - group2_center.Value); int rx = Math.Abs(((ClassicControllerPro)e.state).RJoy.rawX - group3_center.Value); int ry = Math.Abs(((ClassicControllerPro)e.state).RJoy.rawY - group4_center.Value); if (lx > group1_dead.Value) group1_dead.Value = lx; if (ly > group2_dead.Value) group2_dead.Value = ly; if (rx > group3_dead.Value) group3_dead.Value = rx; if (ry > group4_dead.Value) group4_dead.Value = ry; }
                        break;
                    #endregion
                    #region Pro Controller
                    case CalibrationStep.Pro_joy_center:
                        group1_center.Value = ((ProController)e.state).LJoy.rawX; group2_center.Value = ((ProController)e.state).LJoy.rawY;
                        group3_center.Value = ((ProController)e.state).RJoy.rawX; group4_center.Value = ((ProController)e.state).RJoy.rawY;
                        break;
                    case CalibrationStep.Pro_joy_range:
                        if (group1_min.Value == 0) { group1_min.Value = ((ProController)e.state).LJoy.rawX; group1_max.Value = ((ProController)e.state).LJoy.rawX; group2_min.Value = ((ProController)e.state).LJoy.rawY; group2_max.Value = ((ProController)e.state).LJoy.rawY; group3_min.Value = ((ProController)e.state).RJoy.rawX; group3_max.Value = ((ProController)e.state).RJoy.rawX; group4_min.Value = ((ProController)e.state).RJoy.rawY; group4_max.Value = ((ProController)e.state).RJoy.rawY; }
                        else { if (group1_min.Value - 32 > ((ProController)e.state).LJoy.rawX) group1_min.Value = ((ProController)e.state).LJoy.rawX; if (group1_max.Value + 32 < ((ProController)e.state).LJoy.rawX) group1_max.Value = ((ProController)e.state).LJoy.rawX; if (group2_min.Value - 32 > ((ProController)e.state).LJoy.rawY) group2_min.Value = ((ProController)e.state).LJoy.rawY; if (group2_max.Value + 32 < ((ProController)e.state).LJoy.rawY) group2_max.Value = ((ProController)e.state).LJoy.rawY; if (group3_min.Value - 32 > ((ProController)e.state).RJoy.rawX) group3_min.Value = ((ProController)e.state).RJoy.rawX; if (group3_max.Value + 32 < ((ProController)e.state).RJoy.rawX) group3_max.Value = ((ProController)e.state).RJoy.rawX; if (group4_min.Value - 32 > ((ProController)e.state).RJoy.rawY) group4_min.Value = ((ProController)e.state).RJoy.rawY; if (group4_max.Value + 32 < ((ProController)e.state).RJoy.rawY) group4_max.Value = ((ProController)e.state).RJoy.rawY; }
                        break;
                    case CalibrationStep.Pro_joy_deadzone:
                        { int lx = Math.Abs(((ProController)e.state).LJoy.rawX - group1_center.Value); int ly = Math.Abs(((ProController)e.state).LJoy.rawY - group2_center.Value); int rx = Math.Abs(((ProController)e.state).RJoy.rawX - group3_center.Value); int ry = Math.Abs(((ProController)e.state).RJoy.rawY - group4_center.Value); if (lx > group1_dead.Value) group1_dead.Value = lx; if (ly > group2_dead.Value) group2_dead.Value = ly; if (rx > group3_dead.Value) group3_dead.Value = rx; if (ry > group4_dead.Value) group4_dead.Value = ry; }
                        break;
                    #endregion
                }
            });
        }

        private static bool CanHandleStateForStep(CalibrationStep step, INintrollerState state)
        {
            return step switch
            {
                CalibrationStep.Wiimote_acc_x_center
                or CalibrationStep.Wiimote_acc_x_range
                or CalibrationStep.Wiimote_acc_y_center
                or CalibrationStep.Wiimote_acc_y_range
                or CalibrationStep.Wiimote_acc_z_center
                or CalibrationStep.Wiimote_acc_z_range
                    => state is Wiimote,

                CalibrationStep.Nunchuk_acc_x_center
                or CalibrationStep.Nunchuk_acc_x_range
                or CalibrationStep.Nunchuk_acc_y_center
                or CalibrationStep.Nunchuk_acc_y_range
                or CalibrationStep.Nunchuk_acc_z_center
                or CalibrationStep.Nunchuk_acc_z_range
                or CalibrationStep.Nunchuk_acc_done
                or CalibrationStep.Nunchuk_joy_center
                or CalibrationStep.Nunchuk_joy_range
                or CalibrationStep.Nunchuk_joy_deadzone
                    => state is Nunchuk,

                CalibrationStep.Classic_joy_center
                or CalibrationStep.Classic_joy_range
                or CalibrationStep.Classic_joy_deadzone
                    => state is ClassicController,

                CalibrationStep.ClassicPro_joy_center
                or CalibrationStep.ClassicPro_joy_range
                or CalibrationStep.ClassicPro_joy_deadzone
                    => state is ClassicControllerPro,

                CalibrationStep.Pro_joy_center
                or CalibrationStep.Pro_joy_range
                or CalibrationStep.Pro_joy_deadzone
                    => state is ProController,

                _ => true
            };
        }

        private void UpdateUI()
        {
            if (_step != CalibrationStep.ChangeController) nextBtn.Content = "Next";
            switch (_step)
            {
                case CalibrationStep.ChangeController:
                    inst.Text = "Calibration for this controller is done. Plug in another extension or click Done to apply the calibrations."; nextBtn.Content = "Done"; break;
                case CalibrationStep.Done:
                    inst.Text = "Controller calibration complete. You can adjust the values as desired. Click Done to apply the calibration."; nextBtn.Content = "Done"; break;

                case CalibrationStep.Wiimote_acc_x_center:
                    ResetValues(); title.Text = "Wiimote";
                    inst.Text = "Place the Wiimote on a flat surface face down and click Next.";
                    group1_center.Max = group2_center.Max = group3_center.Max = 300; group1_min.Max = group2_min.Max = group3_min.Max = 300; group1_max.Max = group2_max.Max = group3_max.Max = 300; group1_dead.Max = group2_dead.Max = group3_dead.Max = 150;
                    group1Header.Text = "X-Axis"; group1_center.IsEnabled = false; group1_min.IsEnabled = false; group1_max.IsEnabled = false; group1_dead.IsEnabled = false; group1.Visibility = Visibility.Visible;
                    group2Header.Text = "Y-Axis"; group2_center.IsEnabled = false; group2_min.IsEnabled = false; group2_max.IsEnabled = false; group2_dead.IsEnabled = false; group2.Visibility = Visibility.Visible;
                    group3Header.Text = "Z-Axis"; group3_center.IsEnabled = false; group3_min.IsEnabled = false; group3_max.IsEnabled = false; group3_dead.IsEnabled = false; group3.Visibility = Visibility.Visible;
                    group4.Visibility = Visibility.Collapsed; group5.Visibility = Visibility.Visible; group5.IsHitTestVisible = false; groupL.Visibility = Visibility.Collapsed; groupR.Visibility = Visibility.Collapsed; break;
                case CalibrationStep.Wiimote_acc_x_range: inst.Text = "Rotate the Wiimote so that the buttons are facing to the left and then roll it around so the buttons are facing to the right then click Next."; group1_center.IsEnabled = true; break;
                case CalibrationStep.Wiimote_acc_y_center: inst.Text = "Return the Wiimote to a face down position and click Next."; group1_center.IsEnabled = true; group1_min.IsEnabled = true; group1_max.IsEnabled = true; group1_dead.IsEnabled = true; break;
                case CalibrationStep.Wiimote_acc_y_range: inst.Text = "Move the Wiimote so that it is standing on the top (IR Sensor down) and then move it so that it is standing strait up (extension port down) then click Next."; group2_center.IsEnabled = true; break;
                case CalibrationStep.Wiimote_acc_z_center: inst.Text = "Keep the Wiimote standing up on its extension port then click Next."; group2_center.IsEnabled = true; group2_min.IsEnabled = true; group2_max.IsEnabled = true; group2_dead.IsEnabled = true; break;
                case CalibrationStep.Wiimote_acc_z_range: inst.Text = "Lay the Wiimote down so that its buttons are face up and then rotate it around so its buttons are face down then click Next."; group3_center.IsEnabled = true; break;

                case CalibrationStep.Nunchuk_acc_x_center:
                    ResetValues(); title.Text = "Nunchuk";
                    inst.Text = "Hold the Nunchuk right side up with the top of the joystick parallel to the ground. Then click Next.";
                    group1_center.Max = group2_center.Max = group3_center.Max = 300; group1_min.Max = group2_min.Max = group3_min.Max = 300; group1_max.Max = group2_max.Max = group3_max.Max = 300; group1_dead.Max = group2_dead.Max = group3_dead.Max = 150; group1_dead.Value = group2_dead.Value = group3_dead.Value = 16;
                    group1Header.Text = "X-Axis"; group1_center.IsEnabled = false; group1_min.IsEnabled = false; group1_max.IsEnabled = false; group1_dead.IsEnabled = false; group1.Visibility = Visibility.Visible;
                    group2Header.Text = "Y-Axis"; group2_center.IsEnabled = false; group2_min.IsEnabled = false; group2_max.IsEnabled = false; group2_dead.IsEnabled = false; group2.Visibility = Visibility.Visible;
                    group3Header.Text = "Z-Axis"; group3_center.IsEnabled = false; group3_min.IsEnabled = false; group3_max.IsEnabled = false; group3_dead.IsEnabled = false; group3.Visibility = Visibility.Visible;
                    group4.Visibility = Visibility.Collapsed; group5.Visibility = Visibility.Collapsed; groupL.Visibility = Visibility.Collapsed; groupR.Visibility = Visibility.Collapsed; break;
                case CalibrationStep.Nunchuk_acc_x_range: inst.Text = "Roll the Nunchuk left and right to the desired angles and then click Next."; group1_center.IsEnabled = true; break;
                case CalibrationStep.Nunchuk_acc_y_center: inst.Text = "Return the Nunchuk to the face up position and click Next."; group1_center.IsEnabled = true; group1_min.IsEnabled = true; group1_max.IsEnabled = true; group1_dead.IsEnabled = true; break;
                case CalibrationStep.Nunchuk_acc_y_range: inst.Text = "Tilt the Nunchuk up and down to the desired angles and then click Next."; group2_center.IsEnabled = true; break;
                case CalibrationStep.Nunchuk_acc_z_center: inst.Text = "Rotate the Nunchuk so that the top of the joystick is perpendicular to the ground and then click Next."; group2_center.IsEnabled = true; group2_min.IsEnabled = true; group2_max.IsEnabled = true; group2_dead.IsEnabled = true; break;
                case CalibrationStep.Nunchuk_acc_z_range: inst.Text = "Move the Nunchuk face up and then face down and then click Next."; group3_center.IsEnabled = true; break;
                case CalibrationStep.Nunchuk_acc_done: inst.Text = "Accelerometer calibration is complete, click next to calibrate the joystick."; group3_min.IsEnabled = true; group3_max.IsEnabled = true; group3_dead.IsEnabled = true; break;
                case CalibrationStep.Nunchuk_joy_center:
                    ResetValues(); inst.Text = "Keep the joystick untouched and click on Next.";
                    group1_center.Max = group2_center.Max = 300; group1_min.Max = group2_min.Max = 300; group1_max.Max = group2_max.Max = 300; group1_dead.Max = group2_dead.Max = 150; group1_dead.Value = group2_dead.Value = 8;
                    group1Header.Text = "X-Axis"; group1_center.IsEnabled = false; group1_min.IsEnabled = false; group1_max.IsEnabled = false; group1_dead.IsEnabled = false; group1.Visibility = Visibility.Visible;
                    group2Header.Text = "Y-Axis"; group2_center.IsEnabled = false; group2_min.IsEnabled = false; group2_max.IsEnabled = false; group2_dead.IsEnabled = false; group2.Visibility = Visibility.Visible;
                    group3.Visibility = Visibility.Collapsed; group4.Visibility = Visibility.Collapsed; groupL.Visibility = Visibility.Collapsed; groupR.Visibility = Visibility.Collapsed; break;
                case CalibrationStep.Nunchuk_joy_range: inst.Text = "Move the joystick around in a full circle then click Next."; group1_center.IsEnabled = true; group2_center.IsEnabled = true; break;
                case CalibrationStep.Nunchuk_joy_deadzone: inst.Text = "Carefully wiggle the joystick in a circular motion to find the edges of the dead zone and then click Next."; group1_min.IsEnabled = true; group1_max.IsEnabled = true; group2_min.IsEnabled = true; group2_max.IsEnabled = true; break;

                case CalibrationStep.Classic_joy_center:
                    ResetValues(); title.Text = "Classic Controller";
                    inst.Text = "Keep both joysticks untouched and click on Next.";
                    group1_center.Max = group2_center.Max = group3_center.Max = group4_center.Max = 100; group1_min.Max = group2_min.Max = group3_min.Max = group4_min.Max = 100; group1_max.Max = group2_max.Max = group3_max.Max = group4_max.Max = 100; group1_dead.Max = group2_dead.Max = group3_dead.Max = group4_dead.Max = 50;
                    group1Header.Text = "Left X-Axis"; group1_center.IsEnabled = false; group1_min.IsEnabled = false; group1_max.IsEnabled = false; group1_dead.IsEnabled = false; group1.Visibility = Visibility.Visible;
                    group2Header.Text = "Left Y-Axis"; group2_center.IsEnabled = false; group2_min.IsEnabled = false; group2_max.IsEnabled = false; group2_dead.IsEnabled = false; group2.Visibility = Visibility.Visible;
                    group3Header.Text = "Right X-Axis"; group3_center.IsEnabled = false; group3_min.IsEnabled = false; group3_max.IsEnabled = false; group3_dead.IsEnabled = false; group3.Visibility = Visibility.Visible;
                    group4Header.Text = "Right Y-Axis"; group4_center.IsEnabled = false; group4_min.IsEnabled = false; group4_max.IsEnabled = false; group4_dead.IsEnabled = false; group4.Visibility = Visibility.Visible;
                    groupLHeader.Text = "Left Trigger"; groupL_min.IsEnabled = false; groupL_max.IsEnabled = false; groupL.Visibility = Visibility.Visible;
                    groupRHeader.Text = "Right Trigger"; groupR_min.IsEnabled = false; groupR_max.IsEnabled = false; groupR.Visibility = Visibility.Visible;
                    group5.Visibility = Visibility.Collapsed; break;
                case CalibrationStep.Classic_joy_range: inst.Text = "Move both joysticks around in a full circle and press both L & R in completely then click Next."; group1_center.IsEnabled = true; group2_center.IsEnabled = true; group3_center.IsEnabled = true; group4_center.IsEnabled = true; break;
                case CalibrationStep.Classic_joy_deadzone: inst.Text = "Without putting pressure on the joysticks, rub the outside of the joysticks in a circular motion to find the edges of the dead zone and then click Next."; group1_min.IsEnabled = true; group1_max.IsEnabled = true; group2_min.IsEnabled = true; group2_max.IsEnabled = true; group3_min.IsEnabled = true; group3_max.IsEnabled = true; group4_min.IsEnabled = true; group4_max.IsEnabled = true; groupL_min.IsEnabled = true; groupL_max.IsEnabled = true; groupR_min.IsEnabled = true; groupR_max.IsEnabled = true; break;

                case CalibrationStep.ClassicPro_joy_center:
                    ResetValues(); title.Text = "Classic Controller Pro";
                    inst.Text = "Keep both joysticks untouched and click on Next.";
                    group1_center.Max = group2_center.Max = group3_center.Max = group4_center.Max = 100; group1_min.Max = group2_min.Max = group3_min.Max = group4_min.Max = 100; group1_max.Max = group2_max.Max = group3_max.Max = group4_max.Max = 100; group1_dead.Max = group2_dead.Max = group3_dead.Max = group4_dead.Max = 50;
                    group1Header.Text = "Left X-Axis"; group1_center.IsEnabled = false; group1_min.IsEnabled = false; group1_max.IsEnabled = false; group1_dead.IsEnabled = false; group1.Visibility = Visibility.Visible;
                    group2Header.Text = "Left Y-Axis"; group2_center.IsEnabled = false; group2_min.IsEnabled = false; group2_max.IsEnabled = false; group2_dead.IsEnabled = false; group2.Visibility = Visibility.Visible;
                    group3Header.Text = "Right X-Axis"; group3_center.IsEnabled = false; group3_min.IsEnabled = false; group3_max.IsEnabled = false; group3_dead.IsEnabled = false; group3.Visibility = Visibility.Visible;
                    group4Header.Text = "Right Y-Axis"; group4_center.IsEnabled = false; group4_min.IsEnabled = false; group4_max.IsEnabled = false; group4_dead.IsEnabled = false; group4.Visibility = Visibility.Visible;
                    group5.Visibility = Visibility.Collapsed; groupL.Visibility = Visibility.Collapsed; groupR.Visibility = Visibility.Collapsed; break;
                case CalibrationStep.ClassicPro_joy_range:
                case CalibrationStep.Pro_joy_range: inst.Text = "Move both joysticks around in a full circle then click Next."; group1_center.IsEnabled = true; group2_center.IsEnabled = true; group3_center.IsEnabled = true; group4_center.IsEnabled = true; break;
                case CalibrationStep.ClassicPro_joy_deadzone:
                case CalibrationStep.Pro_joy_deadzone: inst.Text = "Without putting pressure on the joysticks, rub the outside of the joysticks in a circular motion to find the edges of the dead zone and then click Next."; group1_min.IsEnabled = true; group1_max.IsEnabled = true; group2_min.IsEnabled = true; group2_max.IsEnabled = true; group3_min.IsEnabled = true; group3_max.IsEnabled = true; group4_min.IsEnabled = true; group4_max.IsEnabled = true; break;

                case CalibrationStep.Pro_joy_center:
                    ResetValues(); title.Text = "Pro Controller";
                    inst.Text = "Keep both joysticks untouched and click on Next.";
                    group1_center.Max = group2_center.Max = group3_center.Max = group4_center.Max = 4000; group1_min.Max = group2_min.Max = group3_min.Max = group4_min.Max = 4000; group1_max.Max = group2_max.Max = group3_max.Max = group4_max.Max = 4000; group1_dead.Max = group2_dead.Max = group3_dead.Max = group4_dead.Max = 500;
                    group1Header.Text = "Left X-Axis"; group1_center.IsEnabled = false; group1_min.IsEnabled = false; group1_max.IsEnabled = false; group1_dead.IsEnabled = false; group1.Visibility = Visibility.Visible;
                    group2Header.Text = "Left Y-Axis"; group2_center.IsEnabled = false; group2_min.IsEnabled = false; group2_max.IsEnabled = false; group2_dead.IsEnabled = false; group2.Visibility = Visibility.Visible;
                    group3Header.Text = "Right X-Axis"; group3_center.IsEnabled = false; group3_min.IsEnabled = false; group3_max.IsEnabled = false; group3_dead.IsEnabled = false; group3.Visibility = Visibility.Visible;
                    group4Header.Text = "Right Y-Axis"; group4_center.IsEnabled = false; group4_min.IsEnabled = false; group4_max.IsEnabled = false; group4_dead.IsEnabled = false; group4.Visibility = Visibility.Visible;
                    group5.Visibility = Visibility.Collapsed; groupL.Visibility = Visibility.Collapsed; groupR.Visibility = Visibility.Collapsed; break;
            }
        }

        public void ResetValues()
        {
            group1_center.Value = group1_min.Value = group1_max.Value = group1_dead.Value = 0;
            group2_center.Value = group2_min.Value = group2_max.Value = group2_dead.Value = 0;
            group3_center.Value = group3_min.Value = group3_max.Value = group3_dead.Value = 0;
            group4_center.Value = group4_min.Value = group4_max.Value = group4_dead.Value = 0;
            groupL_min.Value = groupL_max.Value = 4; groupR_min.Value = groupR_max.Value = 4;
        }

        public void StoreCalibration(ControllerType type)
        {
            switch (type)
            {
                case ControllerType.Wiimote:
                    _calibrations.WiimoteCalibration.accelerometer.centerX = group1_center.Value; _calibrations.WiimoteCalibration.accelerometer.minX = group1_min.Value; _calibrations.WiimoteCalibration.accelerometer.maxX = group1_max.Value; _calibrations.WiimoteCalibration.accelerometer.deadX = group1_dead.Value;
                    _calibrations.WiimoteCalibration.accelerometer.centerY = group2_center.Value; _calibrations.WiimoteCalibration.accelerometer.minY = group2_min.Value; _calibrations.WiimoteCalibration.accelerometer.maxY = group2_max.Value; _calibrations.WiimoteCalibration.accelerometer.deadY = group2_dead.Value;
                    _calibrations.WiimoteCalibration.accelerometer.centerZ = group3_center.Value; _calibrations.WiimoteCalibration.accelerometer.minZ = group3_min.Value; _calibrations.WiimoteCalibration.accelerometer.maxZ = group3_max.Value; _calibrations.WiimoteCalibration.accelerometer.deadZ = group3_dead.Value;
                    _calibrations.WiimoteCalibration.irSensor.boundingArea = new SquareBoundry { center_x = group5_centerX.Value, center_y = group5_centerY.Value, width = group5_width.Value, height = group5_height.Value };
                    _calibratedTypes.Add(ControllerType.Wiimote); break;
                case ControllerType.Nunchuk:
                    _calibrations.NunchukCalibration.accelerometer.centerX = group1_center.Value; _calibrations.NunchukCalibration.accelerometer.minX = group1_min.Value; _calibrations.NunchukCalibration.accelerometer.maxX = group1_max.Value; _calibrations.NunchukCalibration.accelerometer.deadX = group1_dead.Value;
                    _calibrations.NunchukCalibration.accelerometer.centerY = group2_center.Value; _calibrations.NunchukCalibration.accelerometer.minY = group2_min.Value; _calibrations.NunchukCalibration.accelerometer.maxY = group2_max.Value; _calibrations.NunchukCalibration.accelerometer.deadY = group2_dead.Value;
                    _calibrations.NunchukCalibration.accelerometer.centerZ = group3_center.Value; _calibrations.NunchukCalibration.accelerometer.minZ = group3_min.Value; _calibrations.NunchukCalibration.accelerometer.maxZ = group3_max.Value; _calibrations.NunchukCalibration.accelerometer.deadZ = group3_dead.Value;
                    _calibratedTypes.Add(ControllerType.Nunchuk); _calibratedTypes.Add(ControllerType.NunchukB); break;
                case ControllerType.NunchukB:
                    _calibrations.NunchukCalibration.joystick.centerX = group1_center.Value; _calibrations.NunchukCalibration.joystick.minX = group1_min.Value; _calibrations.NunchukCalibration.joystick.maxX = group1_max.Value; _calibrations.NunchukCalibration.joystick.deadX = group1_dead.Value;
                    _calibrations.NunchukCalibration.joystick.centerY = group2_center.Value; _calibrations.NunchukCalibration.joystick.minY = group2_min.Value; _calibrations.NunchukCalibration.joystick.maxY = group2_max.Value; _calibrations.NunchukCalibration.joystick.deadY = group2_dead.Value;
                    _calibratedTypes.Add(ControllerType.Nunchuk); _calibratedTypes.Add(ControllerType.NunchukB); break;
                case ControllerType.ClassicController:
                    _calibrations.ClassicCalibration.LJoy.centerX = group1_center.Value; _calibrations.ClassicCalibration.LJoy.minX = group1_min.Value; _calibrations.ClassicCalibration.LJoy.maxX = group1_max.Value; _calibrations.ClassicCalibration.LJoy.deadX = group1_dead.Value;
                    _calibrations.ClassicCalibration.LJoy.centerY = group2_center.Value; _calibrations.ClassicCalibration.LJoy.minY = group2_min.Value; _calibrations.ClassicCalibration.LJoy.maxY = group2_max.Value; _calibrations.ClassicCalibration.LJoy.deadY = group2_dead.Value;
                    _calibrations.ClassicCalibration.RJoy.centerX = group3_center.Value; _calibrations.ClassicCalibration.RJoy.minX = group3_min.Value; _calibrations.ClassicCalibration.RJoy.maxX = group3_max.Value; _calibrations.ClassicCalibration.RJoy.deadX = group3_dead.Value;
                    _calibrations.ClassicCalibration.RJoy.centerY = group4_center.Value; _calibrations.ClassicCalibration.RJoy.minY = group4_min.Value; _calibrations.ClassicCalibration.RJoy.maxY = group4_max.Value; _calibrations.ClassicCalibration.RJoy.deadY = group4_dead.Value;
                    _calibrations.ClassicCalibration.L.min = groupL_min.Value; _calibrations.ClassicCalibration.L.max = groupL_max.Value;
                    _calibrations.ClassicCalibration.R.min = groupR_min.Value; _calibrations.ClassicCalibration.R.max = groupR_max.Value;
                    _calibratedTypes.Add(ControllerType.ClassicController); break;
                case ControllerType.ClassicControllerPro:
                    _calibrations.ClassicProCalibration.LJoy.centerX = group1_center.Value; _calibrations.ClassicProCalibration.LJoy.minX = group1_min.Value; _calibrations.ClassicProCalibration.LJoy.maxX = group1_max.Value; _calibrations.ClassicProCalibration.LJoy.deadX = group1_dead.Value;
                    _calibrations.ClassicProCalibration.LJoy.centerY = group2_center.Value; _calibrations.ClassicProCalibration.LJoy.minY = group2_min.Value; _calibrations.ClassicProCalibration.LJoy.maxY = group2_max.Value; _calibrations.ClassicProCalibration.LJoy.deadY = group2_dead.Value;
                    _calibrations.ClassicProCalibration.RJoy.centerX = group3_center.Value; _calibrations.ClassicProCalibration.RJoy.minX = group3_min.Value; _calibrations.ClassicProCalibration.RJoy.maxX = group3_max.Value; _calibrations.ClassicProCalibration.RJoy.deadX = group3_dead.Value;
                    _calibrations.ClassicProCalibration.RJoy.centerY = group4_center.Value; _calibrations.ClassicProCalibration.RJoy.minY = group4_min.Value; _calibrations.ClassicProCalibration.RJoy.maxY = group4_max.Value; _calibrations.ClassicProCalibration.RJoy.deadY = group4_dead.Value;
                    _calibratedTypes.Add(ControllerType.ClassicControllerPro); break;
                case ControllerType.ProController:
                    _calibrations.ProCalibration.LJoy.centerX = group1_center.Value; _calibrations.ProCalibration.LJoy.minX = group1_min.Value; _calibrations.ProCalibration.LJoy.maxX = group1_max.Value; _calibrations.ProCalibration.LJoy.deadX = group1_dead.Value;
                    _calibrations.ProCalibration.LJoy.centerY = group2_center.Value; _calibrations.ProCalibration.LJoy.minY = group2_min.Value; _calibrations.ProCalibration.LJoy.maxY = group2_max.Value; _calibrations.ProCalibration.LJoy.deadY = group2_dead.Value;
                    _calibrations.ProCalibration.RJoy.centerX = group3_center.Value; _calibrations.ProCalibration.RJoy.minX = group3_min.Value; _calibrations.ProCalibration.RJoy.maxX = group3_max.Value; _calibrations.ProCalibration.RJoy.deadX = group3_dead.Value;
                    _calibrations.ProCalibration.RJoy.centerY = group4_center.Value; _calibrations.ProCalibration.RJoy.minY = group4_min.Value; _calibrations.ProCalibration.RJoy.maxY = group4_max.Value; _calibrations.ProCalibration.RJoy.deadY = group4_dead.Value;
                    _calibratedTypes.Add(ControllerType.ProController); break;
            }
        }

        private void nextBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_step == CalibrationStep.Done || _step == CalibrationStep.ChangeController)
            { StoreCalibration(_calibrationToSave); doSave = true; Hide(); }
            else
            {
                switch (_step)
                {
                    case CalibrationStep.Wiimote_acc_x_center: _step = CalibrationStep.Wiimote_acc_x_range; break;
                    case CalibrationStep.Wiimote_acc_x_range: _step = CalibrationStep.Wiimote_acc_y_center; break;
                    case CalibrationStep.Wiimote_acc_y_center: _step = CalibrationStep.Wiimote_acc_y_range; break;
                    case CalibrationStep.Wiimote_acc_y_range: _step = CalibrationStep.Wiimote_acc_z_center; break;
                    case CalibrationStep.Wiimote_acc_z_center: _step = CalibrationStep.Wiimote_acc_z_range; break;
                    case CalibrationStep.Wiimote_acc_z_range: _calibrationToSave = ControllerType.Wiimote; _step = CalibrationStep.ChangeController; group3_min.IsEnabled = true; group3_max.IsEnabled = true; group3_dead.IsEnabled = true; group5.IsHitTestVisible = true; break;
                    case CalibrationStep.Nunchuk_acc_x_center: _step = CalibrationStep.Nunchuk_acc_x_range; break;
                    case CalibrationStep.Nunchuk_acc_x_range: _step = CalibrationStep.Nunchuk_acc_y_center; break;
                    case CalibrationStep.Nunchuk_acc_y_center: _step = CalibrationStep.Nunchuk_acc_y_range; break;
                    case CalibrationStep.Nunchuk_acc_y_range: _step = CalibrationStep.Nunchuk_acc_z_center; break;
                    case CalibrationStep.Nunchuk_acc_z_center: _step = CalibrationStep.Nunchuk_acc_z_range; break;
                    case CalibrationStep.Nunchuk_acc_z_range: _calibrationToSave = ControllerType.Nunchuk; _step = CalibrationStep.Nunchuk_acc_done; group1_dead.IsEnabled = true; group2_dead.IsEnabled = true; group3_dead.IsEnabled = true; break;
                    case CalibrationStep.Nunchuk_acc_done: StoreCalibration(ControllerType.Nunchuk); _step = CalibrationStep.Nunchuk_joy_center; break;
                    case CalibrationStep.Nunchuk_joy_center: _step = CalibrationStep.Nunchuk_joy_range; break;
                    case CalibrationStep.Nunchuk_joy_range: _step = CalibrationStep.Nunchuk_joy_deadzone; break;
                    case CalibrationStep.Nunchuk_joy_deadzone: _calibrationToSave = ControllerType.NunchukB; _step = CalibrationStep.ChangeController; group1_dead.IsEnabled = true; group2_dead.IsEnabled = true; break;
                    case CalibrationStep.Classic_joy_center: _step = CalibrationStep.Classic_joy_range; break;
                    case CalibrationStep.Classic_joy_range: _step = CalibrationStep.Classic_joy_deadzone; break;
                    case CalibrationStep.Classic_joy_deadzone: _calibrationToSave = ControllerType.ClassicController; _step = CalibrationStep.ChangeController; group1_dead.IsEnabled = true; group2_dead.IsEnabled = true; group3_dead.IsEnabled = true; group4_dead.IsEnabled = true; break;
                    case CalibrationStep.ClassicPro_joy_center: _step = CalibrationStep.ClassicPro_joy_range; break;
                    case CalibrationStep.ClassicPro_joy_range: _step = CalibrationStep.ClassicPro_joy_deadzone; break;
                    case CalibrationStep.ClassicPro_joy_deadzone: _calibrationToSave = ControllerType.ClassicControllerPro; _step = CalibrationStep.ChangeController; group1_dead.IsEnabled = true; group2_dead.IsEnabled = true; group3_dead.IsEnabled = true; group4_dead.IsEnabled = true; break;
                    case CalibrationStep.Pro_joy_center: _step = CalibrationStep.Pro_joy_range; break;
                    case CalibrationStep.Pro_joy_range: _step = CalibrationStep.Pro_joy_deadzone; break;
                    case CalibrationStep.Pro_joy_deadzone: _calibrationToSave = ControllerType.ProController; _step = CalibrationStep.Done; group1_dead.IsEnabled = true; group2_dead.IsEnabled = true; group3_dead.IsEnabled = true; group4_dead.IsEnabled = true; break;
                }
            }
            UpdateUI();
        }

        private void cancelBtn_Click(object sender, RoutedEventArgs e) => Hide();

        private void skipBtn_Click(object sender, RoutedEventArgs e)
        {
            switch (_step)
            {
                case CalibrationStep.Wiimote_acc_x_center: _step = CalibrationStep.Wiimote_acc_x_range; break;
                case CalibrationStep.Wiimote_acc_x_range: _step = CalibrationStep.Wiimote_acc_y_center; break;
                case CalibrationStep.Wiimote_acc_y_center: _step = CalibrationStep.Wiimote_acc_y_range; break;
                case CalibrationStep.Wiimote_acc_y_range: _step = CalibrationStep.Wiimote_acc_z_center; break;
                case CalibrationStep.Wiimote_acc_z_center: _step = CalibrationStep.Wiimote_acc_z_range; break;
                case CalibrationStep.Wiimote_acc_z_range: _step = CalibrationStep.ChangeController; break;
                case CalibrationStep.Nunchuk_acc_x_center: _step = CalibrationStep.Nunchuk_acc_x_range; break;
                case CalibrationStep.Nunchuk_acc_x_range: _step = CalibrationStep.Nunchuk_acc_y_center; break;
                case CalibrationStep.Nunchuk_acc_y_center: _step = CalibrationStep.Nunchuk_acc_y_range; break;
                case CalibrationStep.Nunchuk_acc_y_range: _step = CalibrationStep.Nunchuk_acc_z_center; break;
                case CalibrationStep.Nunchuk_acc_z_center: _step = CalibrationStep.Nunchuk_acc_z_range; break;
                case CalibrationStep.Nunchuk_acc_z_range: _step = CalibrationStep.Nunchuk_acc_done; group1_dead.IsEnabled = true; group2_dead.IsEnabled = true; group3_dead.IsEnabled = true; break;
                case CalibrationStep.Nunchuk_acc_done: _step = CalibrationStep.Nunchuk_joy_center; break;
                case CalibrationStep.Nunchuk_joy_center: _step = CalibrationStep.Nunchuk_joy_range; break;
                case CalibrationStep.Nunchuk_joy_range: _step = CalibrationStep.Nunchuk_joy_deadzone; break;
                case CalibrationStep.Nunchuk_joy_deadzone: _step = CalibrationStep.ChangeController; group1_dead.IsEnabled = true; group2_dead.IsEnabled = true; break;
                case CalibrationStep.Classic_joy_center: _step = CalibrationStep.Classic_joy_range; break;
                case CalibrationStep.Classic_joy_range: _step = CalibrationStep.Classic_joy_deadzone; break;
                case CalibrationStep.Classic_joy_deadzone: _step = CalibrationStep.ChangeController; break;
                case CalibrationStep.ClassicPro_joy_center: _step = CalibrationStep.ClassicPro_joy_range; break;
                case CalibrationStep.ClassicPro_joy_range: _step = CalibrationStep.ClassicPro_joy_deadzone; break;
                case CalibrationStep.ClassicPro_joy_deadzone: _step = CalibrationStep.ChangeController; break;
                case CalibrationStep.Pro_joy_center: _step = CalibrationStep.Pro_joy_range; break;
                case CalibrationStep.Pro_joy_range: _step = CalibrationStep.Pro_joy_deadzone; break;
                case CalibrationStep.Pro_joy_deadzone: _step = CalibrationStep.Done; break;
            }
        }
    }
}
