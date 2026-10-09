using System;
using System.Collections.Generic;
using NintrollerLib;

namespace WiinUSoft.Services;

/// <summary>Domain workflow for collecting and storing controller calibration samples.</summary>
internal sealed class CalibrationWorkflow
{
    internal enum Step
    {
        Done, ChangeController,
        WiimoteAccXCenter, WiimoteAccXRange, WiimoteAccYCenter, WiimoteAccYRange, WiimoteAccZCenter, WiimoteAccZRange,
        NunchukAccXCenter, NunchukAccXRange, NunchukAccYCenter, NunchukAccYRange, NunchukAccZCenter, NunchukAccZRange,
        NunchukAccDone, NunchukJoyCenter, NunchukJoyRange, NunchukJoyDeadzone,
        ClassicJoyCenter, ClassicJoyRange, ClassicJoyDeadzone,
        ClassicProJoyCenter, ClassicProJoyRange, ClassicProJoyDeadzone,
        ProJoyCenter, ProJoyRange, ProJoyDeadzone
    }

    internal sealed class Values
    {
        public int G1C, G1Min, G1Max, G1Dead, G2C, G2Min, G2Max, G2Dead;
        public int G3C, G3Min, G3Max, G3Dead, G4C, G4Min, G4Max, G4Dead;
        public int LMin = 4, LMax = 4, RMin = 4, RMax = 4;
        public int IrX = 512, IrY = 512, IrWidth = 128, IrHeight = 128;

        public void Reset()
        {
            G1C = G1Min = G1Max = G1Dead = 0; G2C = G2Min = G2Max = G2Dead = 0;
            G3C = G3Min = G3Max = G3Dead = 0; G4C = G4Min = G4Max = G4Dead = 0;
            LMin = LMax = RMin = RMax = 4;
        }
    }

    public CalibrationStorage Calibrations { get; } = new();
    internal HashSet<ControllerType> CalibratedTypes { get; } = new();
    internal Step CurrentStep { get; set; } = Step.ChangeController;
    internal ControllerType PendingType { get; set; } = ControllerType.Unknown;

    internal bool Advance(Values values)
    {
        if (CurrentStep is Step.Done or Step.ChangeController)
        {
            if (PendingType != ControllerType.Unknown) StoreCalibration(PendingType, values);
            return true;
        }

        switch (CurrentStep)
        {
            case Step.WiimoteAccXCenter: CurrentStep = Step.WiimoteAccXRange; break;
            case Step.WiimoteAccXRange: CurrentStep = Step.WiimoteAccYCenter; break;
            case Step.WiimoteAccYCenter: CurrentStep = Step.WiimoteAccYRange; break;
            case Step.WiimoteAccYRange: CurrentStep = Step.WiimoteAccZCenter; break;
            case Step.WiimoteAccZCenter: CurrentStep = Step.WiimoteAccZRange; break;
            case Step.WiimoteAccZRange: PendingType = ControllerType.Wiimote; CurrentStep = Step.ChangeController; break;
            case Step.NunchukAccXCenter: CurrentStep = Step.NunchukAccXRange; break;
            case Step.NunchukAccXRange: CurrentStep = Step.NunchukAccYCenter; break;
            case Step.NunchukAccYCenter: CurrentStep = Step.NunchukAccYRange; break;
            case Step.NunchukAccYRange: CurrentStep = Step.NunchukAccZCenter; break;
            case Step.NunchukAccZCenter: CurrentStep = Step.NunchukAccZRange; break;
            case Step.NunchukAccZRange: PendingType = ControllerType.Nunchuk; CurrentStep = Step.NunchukAccDone; break;
            case Step.NunchukAccDone: StoreCalibration(ControllerType.Nunchuk, values); CurrentStep = Step.NunchukJoyCenter; break;
            case Step.NunchukJoyCenter: CurrentStep = Step.NunchukJoyRange; break;
            case Step.NunchukJoyRange: CurrentStep = Step.NunchukJoyDeadzone; break;
            case Step.NunchukJoyDeadzone: PendingType = ControllerType.NunchukB; CurrentStep = Step.ChangeController; break;
            case Step.ClassicJoyCenter: CurrentStep = Step.ClassicJoyRange; break;
            case Step.ClassicJoyRange: CurrentStep = Step.ClassicJoyDeadzone; break;
            case Step.ClassicJoyDeadzone: PendingType = ControllerType.ClassicController; CurrentStep = Step.ChangeController; break;
            case Step.ClassicProJoyCenter: CurrentStep = Step.ClassicProJoyRange; break;
            case Step.ClassicProJoyRange: CurrentStep = Step.ClassicProJoyDeadzone; break;
            case Step.ClassicProJoyDeadzone: PendingType = ControllerType.ClassicControllerPro; CurrentStep = Step.ChangeController; break;
            case Step.ProJoyCenter: CurrentStep = Step.ProJoyRange; break;
            case Step.ProJoyRange: CurrentStep = Step.ProJoyDeadzone; break;
            case Step.ProJoyDeadzone: PendingType = ControllerType.ProController; CurrentStep = Step.Done; break;
        }
        return false;
    }

    internal void Skip()
    {
        switch (CurrentStep)
        {
            case Step.WiimoteAccXCenter: CurrentStep = Step.WiimoteAccXRange; break;
            case Step.WiimoteAccXRange: CurrentStep = Step.WiimoteAccYCenter; break;
            case Step.WiimoteAccYCenter: CurrentStep = Step.WiimoteAccYRange; break;
            case Step.WiimoteAccYRange: CurrentStep = Step.WiimoteAccZCenter; break;
            case Step.WiimoteAccZCenter: CurrentStep = Step.WiimoteAccZRange; break;
            case Step.WiimoteAccZRange: CurrentStep = Step.ChangeController; break;
            case Step.NunchukAccXCenter: CurrentStep = Step.NunchukAccXRange; break;
            case Step.NunchukAccXRange: CurrentStep = Step.NunchukAccYCenter; break;
            case Step.NunchukAccYCenter: CurrentStep = Step.NunchukAccYRange; break;
            case Step.NunchukAccYRange: CurrentStep = Step.NunchukAccZCenter; break;
            case Step.NunchukAccZCenter: CurrentStep = Step.NunchukAccZRange; break;
            case Step.NunchukAccZRange: CurrentStep = Step.NunchukAccDone; break;
            case Step.NunchukAccDone: CurrentStep = Step.NunchukJoyCenter; break;
            case Step.NunchukJoyCenter: CurrentStep = Step.NunchukJoyRange; break;
            case Step.NunchukJoyRange: CurrentStep = Step.NunchukJoyDeadzone; break;
            case Step.NunchukJoyDeadzone: CurrentStep = Step.ChangeController; break;
            case Step.ClassicJoyCenter: CurrentStep = Step.ClassicJoyRange; break;
            case Step.ClassicJoyRange: CurrentStep = Step.ClassicJoyDeadzone; break;
            case Step.ClassicJoyDeadzone: CurrentStep = Step.ChangeController; break;
            case Step.ClassicProJoyCenter: CurrentStep = Step.ClassicProJoyRange; break;
            case Step.ClassicProJoyRange: CurrentStep = Step.ClassicProJoyDeadzone; break;
            case Step.ClassicProJoyDeadzone: CurrentStep = Step.ChangeController; break;
            case Step.ProJoyCenter: CurrentStep = Step.ProJoyRange; break;
            case Step.ProJoyRange: CurrentStep = Step.ProJoyDeadzone; break;
            case Step.ProJoyDeadzone: CurrentStep = Step.Done; break;
        }
    }

    public void Load(string serialized) => Calibrations.SetCalibrations(serialized);

    internal bool SelectController(ControllerType type, Values values)
    {
        if (CalibratedTypes.Contains(type)) return false;
        if (type != ControllerType.ProController && PendingType != ControllerType.Unknown)
            StoreCalibration(PendingType, values);

        CurrentStep = type switch
        {
            ControllerType.Wiimote => Step.WiimoteAccXCenter,
            ControllerType.Nunchuk or ControllerType.NunchukB => Step.NunchukAccXCenter,
            ControllerType.ClassicController => Step.ClassicJoyCenter,
            ControllerType.ClassicControllerPro => Step.ClassicProJoyCenter,
            ControllerType.ProController => Step.ProJoyCenter,
            _ => CurrentStep
        };
        return true;
    }

    internal static bool CanHandle(Step step, INintrollerState state) => step switch
    {
        Step.WiimoteAccXCenter or Step.WiimoteAccXRange or Step.WiimoteAccYCenter or Step.WiimoteAccYRange or Step.WiimoteAccZCenter or Step.WiimoteAccZRange => state is Wiimote,
        Step.NunchukAccXCenter or Step.NunchukAccXRange or Step.NunchukAccYCenter or Step.NunchukAccYRange or Step.NunchukAccZCenter or Step.NunchukAccZRange or Step.NunchukAccDone or Step.NunchukJoyCenter or Step.NunchukJoyRange or Step.NunchukJoyDeadzone => state is Nunchuk,
        Step.ClassicJoyCenter or Step.ClassicJoyRange or Step.ClassicJoyDeadzone => state is ClassicController,
        Step.ClassicProJoyCenter or Step.ClassicProJoyRange or Step.ClassicProJoyDeadzone => state is ClassicControllerPro,
        Step.ProJoyCenter or Step.ProJoyRange or Step.ProJoyDeadzone => state is ProController,
        _ => true
    };

    internal void ProcessSample(INintrollerState state, Values v)
    {
        switch (CurrentStep)
        {
            case Step.WiimoteAccXCenter: v.G1C = ((Wiimote)state).accelerometer.rawX; break;
            case Step.WiimoteAccXRange: Range(ref v.G1Min, ref v.G1Max, ((Wiimote)state).accelerometer.rawX); break;
            case Step.WiimoteAccYCenter: v.G2C = ((Wiimote)state).accelerometer.rawY; break;
            case Step.WiimoteAccYRange: Range(ref v.G2Min, ref v.G2Max, ((Wiimote)state).accelerometer.rawY); break;
            case Step.WiimoteAccZCenter: v.G3C = ((Wiimote)state).accelerometer.rawZ; break;
            case Step.WiimoteAccZRange: Range(ref v.G3Min, ref v.G3Max, ((Wiimote)state).accelerometer.rawZ); break;
            case Step.NunchukAccXCenter: v.G1C = ((Nunchuk)state).accelerometer.rawX; break;
            case Step.NunchukAccXRange: Range(ref v.G1Min, ref v.G1Max, ((Nunchuk)state).accelerometer.rawX); break;
            case Step.NunchukAccYCenter: v.G2C = ((Nunchuk)state).accelerometer.rawY; break;
            case Step.NunchukAccYRange: Range(ref v.G2Min, ref v.G2Max, ((Nunchuk)state).accelerometer.rawY); break;
            case Step.NunchukAccZCenter: v.G3C = ((Nunchuk)state).accelerometer.rawZ; break;
            case Step.NunchukAccZRange: Range(ref v.G3Min, ref v.G3Max, ((Nunchuk)state).accelerometer.rawZ); break;
            case Step.NunchukJoyCenter: v.G1C = ((Nunchuk)state).joystick.rawX; v.G2C = ((Nunchuk)state).joystick.rawY; break;
            case Step.NunchukJoyRange: Range(ref v.G1Min, ref v.G1Max, ((Nunchuk)state).joystick.rawX); Range(ref v.G2Min, ref v.G2Max, ((Nunchuk)state).joystick.rawY); break;
            case Step.NunchukJoyDeadzone: Dead(ref v.G1Dead, Math.Abs(((Nunchuk)state).joystick.rawX - v.G1C)); Dead(ref v.G2Dead, Math.Abs(((Nunchuk)state).joystick.rawY - v.G2C)); break;
            case Step.ClassicJoyCenter:
                v.G1C = ((ClassicController)state).LJoy.rawX; v.G2C = ((ClassicController)state).LJoy.rawY;
                v.G3C = ((ClassicController)state).RJoy.rawX; v.G4C = ((ClassicController)state).RJoy.rawY;
                v.LMin = ((ClassicController)state).L.rawValue; v.RMin = ((ClassicController)state).R.rawValue; break;
            case Step.ClassicJoyRange:
                var cc = (ClassicController)state; Range(ref v.G1Min, ref v.G1Max, cc.LJoy.rawX); Range(ref v.G2Min, ref v.G2Max, cc.LJoy.rawY); Range(ref v.G3Min, ref v.G3Max, cc.RJoy.rawX); Range(ref v.G4Min, ref v.G4Max, cc.RJoy.rawY); Range(ref v.LMin, ref v.LMax, cc.L.rawValue); Range(ref v.RMin, ref v.RMax, cc.R.rawValue); break;
            case Step.ClassicJoyDeadzone:
                var cd = (ClassicController)state; Dead(ref v.G1Dead, Math.Abs(cd.LJoy.rawX - v.G1C)); Dead(ref v.G2Dead, Math.Abs(cd.LJoy.rawY - v.G2C)); Dead(ref v.G3Dead, Math.Abs(cd.RJoy.rawX - v.G3C)); Dead(ref v.G4Dead, Math.Abs(cd.RJoy.rawY - v.G4C)); break;
            case Step.ClassicProJoyCenter: Center((ClassicControllerPro)state, v); break;
            case Step.ClassicProJoyRange: ProRange((ClassicControllerPro)state, v); break;
            case Step.ClassicProJoyDeadzone: ProDead((ClassicControllerPro)state, v); break;
            case Step.ProJoyCenter: Center((ProController)state, v); break;
            case Step.ProJoyRange: ProRange((ProController)state, v); break;
            case Step.ProJoyDeadzone: ProDead((ProController)state, v); break;
        }
    }

    internal void StoreCalibration(ControllerType type, Values v)
    {
        switch (type)
        {
            case ControllerType.Wiimote:
                var w = Calibrations.WiimoteCalibration;
                w.accelerometer.centerX = v.G1C; w.accelerometer.minX = v.G1Min; w.accelerometer.maxX = v.G1Max; w.accelerometer.deadX = v.G1Dead;
                w.accelerometer.centerY = v.G2C; w.accelerometer.minY = v.G2Min; w.accelerometer.maxY = v.G2Max; w.accelerometer.deadY = v.G2Dead;
                w.accelerometer.centerZ = v.G3C; w.accelerometer.minZ = v.G3Min; w.accelerometer.maxZ = v.G3Max; w.accelerometer.deadZ = v.G3Dead;
                w.irSensor.boundingArea = new SquareBoundry { center_x = v.IrX, center_y = v.IrY, width = v.IrWidth, height = v.IrHeight };
                Calibrations.WiimoteCalibration = w;
                break;
            case ControllerType.Nunchuk:
                var n = Calibrations.NunchukCalibration;
                n.accelerometer.centerX = v.G1C; n.accelerometer.minX = v.G1Min; n.accelerometer.maxX = v.G1Max; n.accelerometer.deadX = v.G1Dead;
                n.accelerometer.centerY = v.G2C; n.accelerometer.minY = v.G2Min; n.accelerometer.maxY = v.G2Max; n.accelerometer.deadY = v.G2Dead;
                n.accelerometer.centerZ = v.G3C; n.accelerometer.minZ = v.G3Min; n.accelerometer.maxZ = v.G3Max; n.accelerometer.deadZ = v.G3Dead;
                Calibrations.NunchukCalibration = n;
                break;
            case ControllerType.NunchukB:
                var nb = Calibrations.NunchukCalibration;
                nb.joystick.centerX = v.G1C; nb.joystick.minX = v.G1Min; nb.joystick.maxX = v.G1Max; nb.joystick.deadX = v.G1Dead;
                nb.joystick.centerY = v.G2C; nb.joystick.minY = v.G2Min; nb.joystick.maxY = v.G2Max; nb.joystick.deadY = v.G2Dead;
                Calibrations.NunchukCalibration = nb;
                break;
            case ControllerType.ClassicController:
                var c = Calibrations.ClassicCalibration;
                c.LJoy.centerX = v.G1C; c.LJoy.minX = v.G1Min; c.LJoy.maxX = v.G1Max; c.LJoy.deadX = v.G1Dead;
                c.LJoy.centerY = v.G2C; c.LJoy.minY = v.G2Min; c.LJoy.maxY = v.G2Max; c.LJoy.deadY = v.G2Dead;
                c.RJoy.centerX = v.G3C; c.RJoy.minX = v.G3Min; c.RJoy.maxX = v.G3Max; c.RJoy.deadX = v.G3Dead;
                c.RJoy.centerY = v.G4C; c.RJoy.minY = v.G4Min; c.RJoy.maxY = v.G4Max; c.RJoy.deadY = v.G4Dead;
                c.L.min = v.LMin; c.L.max = v.LMax; c.R.min = v.RMin; c.R.max = v.RMax;
                Calibrations.ClassicCalibration = c;
                break;
            case ControllerType.ClassicControllerPro:
                var cp = Calibrations.ClassicProCalibration;
                cp.LJoy.centerX = v.G1C; cp.LJoy.minX = v.G1Min; cp.LJoy.maxX = v.G1Max; cp.LJoy.deadX = v.G1Dead;
                cp.LJoy.centerY = v.G2C; cp.LJoy.minY = v.G2Min; cp.LJoy.maxY = v.G2Max; cp.LJoy.deadY = v.G2Dead;
                cp.RJoy.centerX = v.G3C; cp.RJoy.minX = v.G3Min; cp.RJoy.maxX = v.G3Max; cp.RJoy.deadX = v.G3Dead;
                cp.RJoy.centerY = v.G4C; cp.RJoy.minY = v.G4Min; cp.RJoy.maxY = v.G4Max; cp.RJoy.deadY = v.G4Dead;
                Calibrations.ClassicProCalibration = cp;
                break;
            case ControllerType.ProController:
                var p = Calibrations.ProCalibration;
                p.LJoy.centerX = v.G1C; p.LJoy.minX = v.G1Min; p.LJoy.maxX = v.G1Max; p.LJoy.deadX = v.G1Dead;
                p.LJoy.centerY = v.G2C; p.LJoy.minY = v.G2Min; p.LJoy.maxY = v.G2Max; p.LJoy.deadY = v.G2Dead;
                p.RJoy.centerX = v.G3C; p.RJoy.minX = v.G3Min; p.RJoy.maxX = v.G3Max; p.RJoy.deadX = v.G3Dead;
                p.RJoy.centerY = v.G4C; p.RJoy.minY = v.G4Min; p.RJoy.maxY = v.G4Max; p.RJoy.deadY = v.G4Dead;
                Calibrations.ProCalibration = p;
                break;
        }
        CalibratedTypes.Add(type);
        if (type == ControllerType.Nunchuk) CalibratedTypes.Add(ControllerType.NunchukB);
    }

    private static void Center(ClassicController c, Values v) { v.G1C = c.LJoy.rawX; v.G2C = c.LJoy.rawY; v.G3C = c.RJoy.rawX; v.G4C = c.RJoy.rawY; }
    private static void Center(ClassicControllerPro c, Values v) { v.G1C = c.LJoy.rawX; v.G2C = c.LJoy.rawY; v.G3C = c.RJoy.rawX; v.G4C = c.RJoy.rawY; }
    private static void Center(ProController c, Values v) { v.G1C = c.LJoy.rawX; v.G2C = c.LJoy.rawY; v.G3C = c.RJoy.rawX; v.G4C = c.RJoy.rawY; }
    private static void ProRange(ClassicControllerPro c, Values v) { Range(ref v.G1Min, ref v.G1Max, c.LJoy.rawX); Range(ref v.G2Min, ref v.G2Max, c.LJoy.rawY); Range(ref v.G3Min, ref v.G3Max, c.RJoy.rawX); Range(ref v.G4Min, ref v.G4Max, c.RJoy.rawY); }
    private static void ProRange(ProController c, Values v) { Range(ref v.G1Min, ref v.G1Max, c.LJoy.rawX); Range(ref v.G2Min, ref v.G2Max, c.LJoy.rawY); Range(ref v.G3Min, ref v.G3Max, c.RJoy.rawX); Range(ref v.G4Min, ref v.G4Max, c.RJoy.rawY); }
    private static void ProDead(ClassicControllerPro c, Values v) { Dead(ref v.G1Dead, Math.Abs(c.LJoy.rawX - v.G1C)); Dead(ref v.G2Dead, Math.Abs(c.LJoy.rawY - v.G2C)); Dead(ref v.G3Dead, Math.Abs(c.RJoy.rawX - v.G3C)); Dead(ref v.G4Dead, Math.Abs(c.RJoy.rawY - v.G4C)); }
    private static void ProDead(ProController c, Values v) { Dead(ref v.G1Dead, Math.Abs(c.LJoy.rawX - v.G1C)); Dead(ref v.G2Dead, Math.Abs(c.LJoy.rawY - v.G2C)); Dead(ref v.G3Dead, Math.Abs(c.RJoy.rawX - v.G3C)); Dead(ref v.G4Dead, Math.Abs(c.RJoy.rawY - v.G4C)); }
    private static void Range(ref int min, ref int max, int value) { if (max == 0) min = max = value; else { if (value < min) min = value; if (value > max) max = value; } }
    private static void Dead(ref int dead, int value) { if (value > dead) dead = value; }
}
