using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NintrollerLib;
using WiinUSoft.Services;
using WiinUSoft.ViewModels;

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

        private ICalibrationInputSession _input = null!;
        private bool _changingType;
        private CalibrationStorage _calibrations => _workflow.Calibrations;
        private readonly CalibrationViewModel _viewModel = new();
        private readonly CalibrationWorkflow _workflow = new();
        private readonly CalibrationWorkflow.Values _sampleValues = new();

        private CalibrateWindow()
        {
            InitializeComponent();
            DataContext = _viewModel;
            Closed += Dialog_Closed;
        }

        public CalibrateWindow(ICalibrationInputSession input) : this()
        {
            _input = input;
            _workflow.Load(_input.StoredCalibrations);
            SelectStep(_input.DeviceType);
            _input.StateUpdated += _device_StateUpdate;
            _input.ExtensionChanged += _device_ExtensionChange;
        }

        private void Dialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            _input.StateUpdated -= _device_StateUpdate;
            _input.ExtensionChanged -= _device_ExtensionChange;
            _input.Dispose();
        }

        private void SelectStep(ControllerType deviceType)
        {
            ReadSampleValues();
            if (!_workflow.SelectController(deviceType, _sampleValues)) return;
            _changingType = true;
            _viewModel.CalibrationTarget = deviceType;
            _viewModel.CalibratedTypes.Clear();
            foreach (var calibrated in _workflow.CalibratedTypes)
                _viewModel.CalibratedTypes.Add(calibrated);
            UpdateUI();
            _changingType = false;
        }

        void _device_ExtensionChange(object? sender, NintrollerExtensionEventArgs e)
            => DispatcherQueue.TryEnqueue(() => SelectStep(e.controllerType));

        void _device_StateUpdate(object? sender, NintrollerStateEventArgs e)
        {
            if (_changingType) return;
            ProcessSampleThroughWorkflow(e.state);
        }

        private bool ProcessSampleThroughWorkflow(INintrollerState state)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (!CalibrationWorkflow.CanHandle(_workflow.CurrentStep, state))
                    return;

                ReadSampleValues();
                _workflow.ProcessSample(state, _sampleValues);
                WriteSampleValues();
            });
            return true;
        }

        private static CalibrationWorkflow.Step MapStep(CalibrationStep step)
            => step switch
            {
                CalibrationStep.Wiimote_acc_x_center => CalibrationWorkflow.Step.WiimoteAccXCenter,
                CalibrationStep.Wiimote_acc_x_range => CalibrationWorkflow.Step.WiimoteAccXRange,
                CalibrationStep.Wiimote_acc_y_center => CalibrationWorkflow.Step.WiimoteAccYCenter,
                CalibrationStep.Wiimote_acc_y_range => CalibrationWorkflow.Step.WiimoteAccYRange,
                CalibrationStep.Wiimote_acc_z_center => CalibrationWorkflow.Step.WiimoteAccZCenter,
                CalibrationStep.Wiimote_acc_z_range => CalibrationWorkflow.Step.WiimoteAccZRange,
                CalibrationStep.Nunchuk_acc_x_center => CalibrationWorkflow.Step.NunchukAccXCenter,
                CalibrationStep.Nunchuk_acc_x_range => CalibrationWorkflow.Step.NunchukAccXRange,
                CalibrationStep.Nunchuk_acc_y_center => CalibrationWorkflow.Step.NunchukAccYCenter,
                CalibrationStep.Nunchuk_acc_y_range => CalibrationWorkflow.Step.NunchukAccYRange,
                CalibrationStep.Nunchuk_acc_z_center => CalibrationWorkflow.Step.NunchukAccZCenter,
                CalibrationStep.Nunchuk_acc_z_range => CalibrationWorkflow.Step.NunchukAccZRange,
                CalibrationStep.Nunchuk_acc_done => CalibrationWorkflow.Step.NunchukAccDone,
                CalibrationStep.Nunchuk_joy_center => CalibrationWorkflow.Step.NunchukJoyCenter,
                CalibrationStep.Nunchuk_joy_range => CalibrationWorkflow.Step.NunchukJoyRange,
                CalibrationStep.Nunchuk_joy_deadzone => CalibrationWorkflow.Step.NunchukJoyDeadzone,
                CalibrationStep.Classic_joy_center => CalibrationWorkflow.Step.ClassicJoyCenter,
                CalibrationStep.Classic_joy_range => CalibrationWorkflow.Step.ClassicJoyRange,
                CalibrationStep.Classic_joy_deadzone => CalibrationWorkflow.Step.ClassicJoyDeadzone,
                CalibrationStep.ClassicPro_joy_center => CalibrationWorkflow.Step.ClassicProJoyCenter,
                CalibrationStep.ClassicPro_joy_range => CalibrationWorkflow.Step.ClassicProJoyRange,
                CalibrationStep.ClassicPro_joy_deadzone => CalibrationWorkflow.Step.ClassicProJoyDeadzone,
                CalibrationStep.Pro_joy_center => CalibrationWorkflow.Step.ProJoyCenter,
                CalibrationStep.Pro_joy_range => CalibrationWorkflow.Step.ProJoyRange,
                CalibrationStep.Pro_joy_deadzone => CalibrationWorkflow.Step.ProJoyDeadzone,
                _ => CalibrationWorkflow.Step.ChangeController
            };

        private static CalibrationStep MapStep(CalibrationWorkflow.Step step)
            => step switch
            {
                CalibrationWorkflow.Step.WiimoteAccXCenter => CalibrationStep.Wiimote_acc_x_center,
                CalibrationWorkflow.Step.WiimoteAccXRange => CalibrationStep.Wiimote_acc_x_range,
                CalibrationWorkflow.Step.WiimoteAccYCenter => CalibrationStep.Wiimote_acc_y_center,
                CalibrationWorkflow.Step.WiimoteAccYRange => CalibrationStep.Wiimote_acc_y_range,
                CalibrationWorkflow.Step.WiimoteAccZCenter => CalibrationStep.Wiimote_acc_z_center,
                CalibrationWorkflow.Step.WiimoteAccZRange => CalibrationStep.Wiimote_acc_z_range,
                CalibrationWorkflow.Step.NunchukAccXCenter => CalibrationStep.Nunchuk_acc_x_center,
                CalibrationWorkflow.Step.NunchukAccXRange => CalibrationStep.Nunchuk_acc_x_range,
                CalibrationWorkflow.Step.NunchukAccYCenter => CalibrationStep.Nunchuk_acc_y_center,
                CalibrationWorkflow.Step.NunchukAccYRange => CalibrationStep.Nunchuk_acc_y_range,
                CalibrationWorkflow.Step.NunchukAccZCenter => CalibrationStep.Nunchuk_acc_z_center,
                CalibrationWorkflow.Step.NunchukAccZRange => CalibrationStep.Nunchuk_acc_z_range,
                CalibrationWorkflow.Step.NunchukAccDone => CalibrationStep.Nunchuk_acc_done,
                CalibrationWorkflow.Step.NunchukJoyCenter => CalibrationStep.Nunchuk_joy_center,
                CalibrationWorkflow.Step.NunchukJoyRange => CalibrationStep.Nunchuk_joy_range,
                CalibrationWorkflow.Step.NunchukJoyDeadzone => CalibrationStep.Nunchuk_joy_deadzone,
                CalibrationWorkflow.Step.ClassicJoyCenter => CalibrationStep.Classic_joy_center,
                CalibrationWorkflow.Step.ClassicJoyRange => CalibrationStep.Classic_joy_range,
                CalibrationWorkflow.Step.ClassicJoyDeadzone => CalibrationStep.Classic_joy_deadzone,
                CalibrationWorkflow.Step.ClassicProJoyCenter => CalibrationStep.ClassicPro_joy_center,
                CalibrationWorkflow.Step.ClassicProJoyRange => CalibrationStep.ClassicPro_joy_range,
                CalibrationWorkflow.Step.ClassicProJoyDeadzone => CalibrationStep.ClassicPro_joy_deadzone,
                CalibrationWorkflow.Step.ProJoyCenter => CalibrationStep.Pro_joy_center,
                CalibrationWorkflow.Step.ProJoyRange => CalibrationStep.Pro_joy_range,
                CalibrationWorkflow.Step.ProJoyDeadzone => CalibrationStep.Pro_joy_deadzone,
                CalibrationWorkflow.Step.Done => CalibrationStep.Done,
                _ => CalibrationStep.ChangeController
            };

        private void ReadSampleValues()
        {
            _sampleValues.G1C = group1_center.Value; _sampleValues.G1Min = group1_min.Value; _sampleValues.G1Max = group1_max.Value; _sampleValues.G1Dead = group1_dead.Value;
            _sampleValues.G2C = group2_center.Value; _sampleValues.G2Min = group2_min.Value; _sampleValues.G2Max = group2_max.Value; _sampleValues.G2Dead = group2_dead.Value;
            _sampleValues.G3C = group3_center.Value; _sampleValues.G3Min = group3_min.Value; _sampleValues.G3Max = group3_max.Value; _sampleValues.G3Dead = group3_dead.Value;
            _sampleValues.G4C = group4_center.Value; _sampleValues.G4Min = group4_min.Value; _sampleValues.G4Max = group4_max.Value; _sampleValues.G4Dead = group4_dead.Value;
            _sampleValues.LMin = groupL_min.Value; _sampleValues.LMax = groupL_max.Value; _sampleValues.RMin = groupR_min.Value; _sampleValues.RMax = groupR_max.Value;
        }

        private void WriteSampleValues()
        {
            group1_center.Value = _sampleValues.G1C; group1_min.Value = _sampleValues.G1Min; group1_max.Value = _sampleValues.G1Max; group1_dead.Value = _sampleValues.G1Dead;
            group2_center.Value = _sampleValues.G2C; group2_min.Value = _sampleValues.G2Min; group2_max.Value = _sampleValues.G2Max; group2_dead.Value = _sampleValues.G2Dead;
            group3_center.Value = _sampleValues.G3C; group3_min.Value = _sampleValues.G3Min; group3_max.Value = _sampleValues.G3Max; group3_dead.Value = _sampleValues.G3Dead;
            group4_center.Value = _sampleValues.G4C; group4_min.Value = _sampleValues.G4Min; group4_max.Value = _sampleValues.G4Max; group4_dead.Value = _sampleValues.G4Dead;
            groupL_min.Value = _sampleValues.LMin; groupL_max.Value = _sampleValues.LMax; groupR_min.Value = _sampleValues.RMin; groupR_max.Value = _sampleValues.RMax;
        }

        private void UpdateUI()
        {
            var step = MapStep(_workflow.CurrentStep);
            _viewModel.NextButtonText = step == CalibrationStep.ChangeController ? "Done" : "Next";
            switch (step)
            {
                case CalibrationStep.ChangeController:
                    _viewModel.Instructions = "Calibration for this controller is done. Plug in another extension or click Done to apply the calibrations."; break;
                case CalibrationStep.Done:
                    _viewModel.Instructions = "Controller calibration complete. You can adjust the values as desired. Click Done to apply the calibration."; break;

                case CalibrationStep.Wiimote_acc_x_center:
                    ResetValues(); _viewModel.Title = "Wiimote";
                    _viewModel.Instructions = "Place the Wiimote on a flat surface face down and click Next.";
                    group1_center.Max = group2_center.Max = group3_center.Max = 300; group1_min.Max = group2_min.Max = group3_min.Max = 300; group1_max.Max = group2_max.Max = group3_max.Max = 300; group1_dead.Max = group2_dead.Max = group3_dead.Max = 150;
                    group1Header.Text = "X-Axis"; group1_center.IsEnabled = false; group1_min.IsEnabled = false; group1_max.IsEnabled = false; group1_dead.IsEnabled = false; group1.Visibility = Visibility.Visible;
                    group2Header.Text = "Y-Axis"; group2_center.IsEnabled = false; group2_min.IsEnabled = false; group2_max.IsEnabled = false; group2_dead.IsEnabled = false; group2.Visibility = Visibility.Visible;
                    group3Header.Text = "Z-Axis"; group3_center.IsEnabled = false; group3_min.IsEnabled = false; group3_max.IsEnabled = false; group3_dead.IsEnabled = false; group3.Visibility = Visibility.Visible;
                    group4.Visibility = Visibility.Collapsed; group5.Visibility = Visibility.Visible; group5.IsHitTestVisible = false; groupL.Visibility = Visibility.Collapsed; groupR.Visibility = Visibility.Collapsed; break;
                case CalibrationStep.Wiimote_acc_x_range: _viewModel.Instructions = "Rotate the Wiimote so that the buttons are facing to the left and then roll it around so the buttons are facing to the right then click Next."; group1_center.IsEnabled = true; break;
                case CalibrationStep.Wiimote_acc_y_center: _viewModel.Instructions = "Return the Wiimote to a face down position and click Next."; group1_center.IsEnabled = true; group1_min.IsEnabled = true; group1_max.IsEnabled = true; group1_dead.IsEnabled = true; break;
                case CalibrationStep.Wiimote_acc_y_range: _viewModel.Instructions = "Move the Wiimote so that it is standing on the top (IR Sensor down) and then move it so that it is standing strait up (extension port down) then click Next."; group2_center.IsEnabled = true; break;
                case CalibrationStep.Wiimote_acc_z_center: _viewModel.Instructions = "Keep the Wiimote standing up on its extension port then click Next."; group2_center.IsEnabled = true; group2_min.IsEnabled = true; group2_max.IsEnabled = true; group2_dead.IsEnabled = true; break;
                case CalibrationStep.Wiimote_acc_z_range: _viewModel.Instructions = "Lay the Wiimote down so that its buttons are face up and then rotate it around so its buttons are face down then click Next."; group3_center.IsEnabled = true; break;

                case CalibrationStep.Nunchuk_acc_x_center:
                    ResetValues(); _viewModel.Title = "Nunchuk";
                    _viewModel.Instructions = "Hold the Nunchuk right side up with the top of the joystick parallel to the ground. Then click Next.";
                    group1_center.Max = group2_center.Max = group3_center.Max = 300; group1_min.Max = group2_min.Max = group3_min.Max = 300; group1_max.Max = group2_max.Max = group3_max.Max = 300; group1_dead.Max = group2_dead.Max = group3_dead.Max = 150; group1_dead.Value = group2_dead.Value = group3_dead.Value = 16;
                    group1Header.Text = "X-Axis"; group1_center.IsEnabled = false; group1_min.IsEnabled = false; group1_max.IsEnabled = false; group1_dead.IsEnabled = false; group1.Visibility = Visibility.Visible;
                    group2Header.Text = "Y-Axis"; group2_center.IsEnabled = false; group2_min.IsEnabled = false; group2_max.IsEnabled = false; group2_dead.IsEnabled = false; group2.Visibility = Visibility.Visible;
                    group3Header.Text = "Z-Axis"; group3_center.IsEnabled = false; group3_min.IsEnabled = false; group3_max.IsEnabled = false; group3_dead.IsEnabled = false; group3.Visibility = Visibility.Visible;
                    group4.Visibility = Visibility.Collapsed; group5.Visibility = Visibility.Collapsed; groupL.Visibility = Visibility.Collapsed; groupR.Visibility = Visibility.Collapsed; break;
                case CalibrationStep.Nunchuk_acc_x_range: _viewModel.Instructions = "Roll the Nunchuk left and right to the desired angles and then click Next."; group1_center.IsEnabled = true; break;
                case CalibrationStep.Nunchuk_acc_y_center: _viewModel.Instructions = "Return the Nunchuk to the face up position and click Next."; group1_center.IsEnabled = true; group1_min.IsEnabled = true; group1_max.IsEnabled = true; group1_dead.IsEnabled = true; break;
                case CalibrationStep.Nunchuk_acc_y_range: _viewModel.Instructions = "Tilt the Nunchuk up and down to the desired angles and then click Next."; group2_center.IsEnabled = true; break;
                case CalibrationStep.Nunchuk_acc_z_center: _viewModel.Instructions = "Rotate the Nunchuk so that the top of the joystick is perpendicular to the ground and then click Next."; group2_center.IsEnabled = true; group2_min.IsEnabled = true; group2_max.IsEnabled = true; group2_dead.IsEnabled = true; break;
                case CalibrationStep.Nunchuk_acc_z_range: _viewModel.Instructions = "Move the Nunchuk face up and then face down and then click Next."; group3_center.IsEnabled = true; break;
                case CalibrationStep.Nunchuk_acc_done: _viewModel.Instructions = "Accelerometer calibration is complete, click next to calibrate the joystick."; group3_min.IsEnabled = true; group3_max.IsEnabled = true; group3_dead.IsEnabled = true; break;
                case CalibrationStep.Nunchuk_joy_center:
                    ResetValues(); _viewModel.Instructions = "Keep the joystick untouched and click on Next.";
                    group1_center.Max = group2_center.Max = 300; group1_min.Max = group2_min.Max = 300; group1_max.Max = group2_max.Max = 300; group1_dead.Max = group2_dead.Max = 150; group1_dead.Value = group2_dead.Value = 8;
                    group1Header.Text = "X-Axis"; group1_center.IsEnabled = false; group1_min.IsEnabled = false; group1_max.IsEnabled = false; group1_dead.IsEnabled = false; group1.Visibility = Visibility.Visible;
                    group2Header.Text = "Y-Axis"; group2_center.IsEnabled = false; group2_min.IsEnabled = false; group2_max.IsEnabled = false; group2_dead.IsEnabled = false; group2.Visibility = Visibility.Visible;
                    group3.Visibility = Visibility.Collapsed; group4.Visibility = Visibility.Collapsed; groupL.Visibility = Visibility.Collapsed; groupR.Visibility = Visibility.Collapsed; break;
                case CalibrationStep.Nunchuk_joy_range: _viewModel.Instructions = "Move the joystick around in a full circle then click Next."; group1_center.IsEnabled = true; group2_center.IsEnabled = true; break;
                case CalibrationStep.Nunchuk_joy_deadzone: _viewModel.Instructions = "Carefully wiggle the joystick in a circular motion to find the edges of the dead zone and then click Next."; group1_min.IsEnabled = true; group1_max.IsEnabled = true; group2_min.IsEnabled = true; group2_max.IsEnabled = true; break;

                case CalibrationStep.Classic_joy_center:
                    ResetValues(); _viewModel.Title = "Classic Controller";
                    _viewModel.Instructions = "Keep both joysticks untouched and click on Next.";
                    group1_center.Max = group2_center.Max = group3_center.Max = group4_center.Max = 100; group1_min.Max = group2_min.Max = group3_min.Max = group4_min.Max = 100; group1_max.Max = group2_max.Max = group3_max.Max = group4_max.Max = 100; group1_dead.Max = group2_dead.Max = group3_dead.Max = group4_dead.Max = 50;
                    group1Header.Text = "Left X-Axis"; group1_center.IsEnabled = false; group1_min.IsEnabled = false; group1_max.IsEnabled = false; group1_dead.IsEnabled = false; group1.Visibility = Visibility.Visible;
                    group2Header.Text = "Left Y-Axis"; group2_center.IsEnabled = false; group2_min.IsEnabled = false; group2_max.IsEnabled = false; group2_dead.IsEnabled = false; group2.Visibility = Visibility.Visible;
                    group3Header.Text = "Right X-Axis"; group3_center.IsEnabled = false; group3_min.IsEnabled = false; group3_max.IsEnabled = false; group3_dead.IsEnabled = false; group3.Visibility = Visibility.Visible;
                    group4Header.Text = "Right Y-Axis"; group4_center.IsEnabled = false; group4_min.IsEnabled = false; group4_max.IsEnabled = false; group4_dead.IsEnabled = false; group4.Visibility = Visibility.Visible;
                    groupLHeader.Text = "Left Trigger"; groupL_min.IsEnabled = false; groupL_max.IsEnabled = false; groupL.Visibility = Visibility.Visible;
                    groupRHeader.Text = "Right Trigger"; groupR_min.IsEnabled = false; groupR_max.IsEnabled = false; groupR.Visibility = Visibility.Visible;
                    group5.Visibility = Visibility.Collapsed; break;
                case CalibrationStep.Classic_joy_range: _viewModel.Instructions = "Move both joysticks around in a full circle and press both L & R in completely then click Next."; group1_center.IsEnabled = true; group2_center.IsEnabled = true; group3_center.IsEnabled = true; group4_center.IsEnabled = true; break;
                case CalibrationStep.Classic_joy_deadzone: _viewModel.Instructions = "Without putting pressure on the joysticks, rub the outside of the joysticks in a circular motion to find the edges of the dead zone and then click Next."; group1_min.IsEnabled = true; group1_max.IsEnabled = true; group2_min.IsEnabled = true; group2_max.IsEnabled = true; group3_min.IsEnabled = true; group3_max.IsEnabled = true; group4_min.IsEnabled = true; group4_max.IsEnabled = true; groupL_min.IsEnabled = true; groupL_max.IsEnabled = true; groupR_min.IsEnabled = true; groupR_max.IsEnabled = true; break;

                case CalibrationStep.ClassicPro_joy_center:
                    ResetValues(); _viewModel.Title = "Classic Controller Pro";
                    _viewModel.Instructions = "Keep both joysticks untouched and click on Next.";
                    group1_center.Max = group2_center.Max = group3_center.Max = group4_center.Max = 100; group1_min.Max = group2_min.Max = group3_min.Max = group4_min.Max = 100; group1_max.Max = group2_max.Max = group3_max.Max = group4_max.Max = 100; group1_dead.Max = group2_dead.Max = group3_dead.Max = group4_dead.Max = 50;
                    group1Header.Text = "Left X-Axis"; group1_center.IsEnabled = false; group1_min.IsEnabled = false; group1_max.IsEnabled = false; group1_dead.IsEnabled = false; group1.Visibility = Visibility.Visible;
                    group2Header.Text = "Left Y-Axis"; group2_center.IsEnabled = false; group2_min.IsEnabled = false; group2_max.IsEnabled = false; group2_dead.IsEnabled = false; group2.Visibility = Visibility.Visible;
                    group3Header.Text = "Right X-Axis"; group3_center.IsEnabled = false; group3_min.IsEnabled = false; group3_max.IsEnabled = false; group3_dead.IsEnabled = false; group3.Visibility = Visibility.Visible;
                    group4Header.Text = "Right Y-Axis"; group4_center.IsEnabled = false; group4_min.IsEnabled = false; group4_max.IsEnabled = false; group4_dead.IsEnabled = false; group4.Visibility = Visibility.Visible;
                    group5.Visibility = Visibility.Collapsed; groupL.Visibility = Visibility.Collapsed; groupR.Visibility = Visibility.Collapsed; break;
                case CalibrationStep.ClassicPro_joy_range:
                case CalibrationStep.Pro_joy_range: _viewModel.Instructions = "Move both joysticks around in a full circle then click Next."; group1_center.IsEnabled = true; group2_center.IsEnabled = true; group3_center.IsEnabled = true; group4_center.IsEnabled = true; break;
                case CalibrationStep.ClassicPro_joy_deadzone:
                case CalibrationStep.Pro_joy_deadzone: _viewModel.Instructions = "Without putting pressure on the joysticks, rub the outside of the joysticks in a circular motion to find the edges of the dead zone and then click Next."; group1_min.IsEnabled = true; group1_max.IsEnabled = true; group2_min.IsEnabled = true; group2_max.IsEnabled = true; group3_min.IsEnabled = true; group3_max.IsEnabled = true; group4_min.IsEnabled = true; group4_max.IsEnabled = true; break;

                case CalibrationStep.Pro_joy_center:
                    ResetValues(); _viewModel.Title = "Pro Controller";
                    _viewModel.Instructions = "Keep both joysticks untouched and click on Next.";
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
            ReadSampleValues();
            _workflow.StoreCalibration(type, _sampleValues);
            _viewModel.CalibratedTypes.Add(type);
        }

        private void nextBtn_Click(object sender, RoutedEventArgs e)
        {
            ReadSampleValues();
            bool done = _workflow.Advance(_sampleValues);
            if (done)
            {
                _viewModel.DoSave = true;
                doSave = true;
                Hide();
                return;
            }
            UpdateUI();
        }

        private void cancelBtn_Click(object sender, RoutedEventArgs e) => Hide();

        private void skipBtn_Click(object sender, RoutedEventArgs e)
        {
            _workflow.Skip();
            UpdateUI();
        }

    }
}
