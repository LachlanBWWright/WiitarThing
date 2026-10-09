using NintrollerLib;
using WiinUSoft.Holders;
using WiinUSoft.VirtualOutput;

namespace WiinUSoft.Services;

/// <summary>Maps Wiimote-family state into holder inputs without view dependencies.</summary>
internal sealed class ControllerInputMapper
{
    public IR ApplyNonGuitar(Holder holder, ControllerType controllerType, object state, bool snapIrPointer, Property properties, IR previousIr)
    {
        switch (controllerType)
        {
            case ControllerType.ProController:
                ApplyProController(holder, (ProController)state);
                break;
            case ControllerType.Wiimote:
                previousIr = ApplyWiimote(holder, (Wiimote)state, snapIrPointer, properties, previousIr);
                break;
            case ControllerType.Nunchuk:
            case ControllerType.NunchukB:
                var nunchuk = (Nunchuk)state;
                previousIr = ApplyWiimote(holder, nunchuk.wiimote, snapIrPointer, properties, previousIr);
                ApplyNunchuk(holder, nunchuk);
                break;
            case ControllerType.ClassicController:
                var classic = (ClassicController)state;
                previousIr = ApplyWiimote(holder, classic.wiimote, snapIrPointer, properties, previousIr);
                ApplyClassic(holder, classic);
                break;
            case ControllerType.ClassicControllerPro:
                var classicPro = (ClassicControllerPro)state;
                previousIr = ApplyWiimote(holder, classicPro.wiimote, snapIrPointer, properties, previousIr);
                ApplyClassicPro(holder, classicPro);
                break;
            case ControllerType.Drums:
                ApplyDrums(holder, (WiiDrums)state);
                break;
        }

        return previousIr;
    }

    public ControllerOutputState ApplyGuitar(Holder? holder, WiiGuitar guitar)
    {
        if (holder is not null)
        {
            holder.SetValue(Inputs.WiiGuitar.G, guitar.G); holder.SetValue(Inputs.WiiGuitar.R, guitar.R);
            holder.SetValue(Inputs.WiiGuitar.Y, guitar.Y); holder.SetValue(Inputs.WiiGuitar.B, guitar.B);
            holder.SetValue(Inputs.WiiGuitar.O, guitar.O);
            holder.SetValue(Inputs.WiiGuitar.UP, guitar.Up); holder.SetValue(Inputs.WiiGuitar.DOWN, guitar.Down);
            holder.SetValue(Inputs.WiiGuitar.LEFT, guitar.Left); holder.SetValue(Inputs.WiiGuitar.RIGHT, guitar.Right);
            holder.SetValue(Inputs.WiiGuitar.WHAMMYHIGH, guitar.WhammyHigh); holder.SetValue(Inputs.WiiGuitar.WHAMMYLOW, guitar.WhammyLow);
            holder.SetValue(Inputs.WiiGuitar.TILTHIGH, guitar.TiltHigh); holder.SetValue(Inputs.WiiGuitar.TILTLOW, guitar.TiltLow);
            holder.SetValue(Inputs.WiiGuitar.START, guitar.Start); holder.SetValue(Inputs.WiiGuitar.SELECT, guitar.Select);
        }

        return ControllerOutputState.FromWiiGuitar(guitar);
    }

    public IR ApplyWiimote(Holder inputHolder, Wiimote wiimote, bool snapIrPointer, Property properties, IR previousIr)
    {
        wiimote.irSensor.Normalize();
        inputHolder.SetValue(Inputs.Wiimote.A, wiimote.buttons.A); inputHolder.SetValue(Inputs.Wiimote.B, wiimote.buttons.B);
        inputHolder.SetValue(Inputs.Wiimote.ONE, wiimote.buttons.One); inputHolder.SetValue(Inputs.Wiimote.TWO, wiimote.buttons.Two);
        inputHolder.SetValue(Inputs.Wiimote.UP, wiimote.buttons.Up); inputHolder.SetValue(Inputs.Wiimote.DOWN, wiimote.buttons.Down);
        inputHolder.SetValue(Inputs.Wiimote.LEFT, wiimote.buttons.Left); inputHolder.SetValue(Inputs.Wiimote.RIGHT, wiimote.buttons.Right);
        inputHolder.SetValue(Inputs.Wiimote.MINUS, wiimote.buttons.Minus); inputHolder.SetValue(Inputs.Wiimote.PLUS, wiimote.buttons.Plus);
        inputHolder.SetValue(Inputs.Wiimote.HOME, wiimote.buttons.Home);
        inputHolder.SetValue(Inputs.Wiimote.TILT_RIGHT, wiimote.accelerometer.X > 0 ? wiimote.accelerometer.X : 0);
        inputHolder.SetValue(Inputs.Wiimote.TILT_LEFT, wiimote.accelerometer.X < 0 ? wiimote.accelerometer.X : 0);
        inputHolder.SetValue(Inputs.Wiimote.TILT_UP, wiimote.accelerometer.Y > 0 ? wiimote.accelerometer.Y : 0);
        inputHolder.SetValue(Inputs.Wiimote.TILT_DOWN, wiimote.accelerometer.Y < 0 ? wiimote.accelerometer.Y : 0);
        inputHolder.SetValue(Inputs.Wiimote.ACC_SHAKE_X, wiimote.accelerometer.X > 1.15);
        inputHolder.SetValue(Inputs.Wiimote.ACC_SHAKE_Y, wiimote.accelerometer.Y > 1.15);
        inputHolder.SetValue(Inputs.Wiimote.ACC_SHAKE_Z, wiimote.accelerometer.Z > 1.15);

        if (snapIrPointer && !wiimote.irSensor.point1.visible && !wiimote.irSensor.point2.visible)
        {
            if (properties.pointerMode is Property.PointerOffScreenMode.SnapX or Property.PointerOffScreenMode.SnapXY)
                wiimote.irSensor.X = previousIr.X;
            if (properties.pointerMode is Property.PointerOffScreenMode.SnapY or Property.PointerOffScreenMode.SnapXY)
                wiimote.irSensor.Y = previousIr.Y;
        }

        inputHolder.SetValue(Inputs.Wiimote.IR_RIGHT, wiimote.irSensor.X > 0 ? wiimote.irSensor.X : 0);
        inputHolder.SetValue(Inputs.Wiimote.IR_LEFT, wiimote.irSensor.X < 0 ? wiimote.irSensor.X : 0);
        inputHolder.SetValue(Inputs.Wiimote.IR_UP, wiimote.irSensor.Y > 0 ? wiimote.irSensor.Y : 0);
        inputHolder.SetValue(Inputs.Wiimote.IR_DOWN, wiimote.irSensor.Y < 0 ? wiimote.irSensor.Y : 0);
        return wiimote.irSensor;
    }

    public void ApplyProController(Holder holder, ProController controller)
    {
        holder.SetValue(Inputs.ProController.A, controller.A); holder.SetValue(Inputs.ProController.B, controller.B);
        holder.SetValue(Inputs.ProController.X, controller.X); holder.SetValue(Inputs.ProController.Y, controller.Y);
        holder.SetValue(Inputs.ProController.UP, controller.Up); holder.SetValue(Inputs.ProController.DOWN, controller.Down);
        holder.SetValue(Inputs.ProController.LEFT, controller.Left); holder.SetValue(Inputs.ProController.RIGHT, controller.Right);
        holder.SetValue(Inputs.ProController.L, controller.L); holder.SetValue(Inputs.ProController.R, controller.R);
        holder.SetValue(Inputs.ProController.ZL, controller.ZL); holder.SetValue(Inputs.ProController.ZR, controller.ZR);
        holder.SetValue(Inputs.ProController.START, controller.Plus); holder.SetValue(Inputs.ProController.SELECT, controller.Minus);
        holder.SetValue(Inputs.ProController.HOME, controller.Home); holder.SetValue(Inputs.ProController.LS, controller.LStick);
        holder.SetValue(Inputs.ProController.RS, controller.RStick);
        holder.SetValue(Inputs.ProController.LRIGHT, controller.LJoy.X > 0 ? controller.LJoy.X : 0f);
        holder.SetValue(Inputs.ProController.LLEFT, controller.LJoy.X < 0 ? -controller.LJoy.X : 0f);
        holder.SetValue(Inputs.ProController.LUP, controller.LJoy.Y > 0 ? controller.LJoy.Y : 0f);
        holder.SetValue(Inputs.ProController.LDOWN, controller.LJoy.Y < 0 ? -controller.LJoy.Y : 0f);
        holder.SetValue(Inputs.ProController.RRIGHT, controller.RJoy.X > 0 ? controller.RJoy.X : 0f);
        holder.SetValue(Inputs.ProController.RLEFT, controller.RJoy.X < 0 ? -controller.RJoy.X : 0f);
        holder.SetValue(Inputs.ProController.RUP, controller.RJoy.Y > 0 ? controller.RJoy.Y : 0f);
        holder.SetValue(Inputs.ProController.RDOWN, controller.RJoy.Y < 0 ? -controller.RJoy.Y : 0f);
    }

    public void ApplyNunchuk(Holder holder, Nunchuk controller)
    {
        holder.SetValue(Inputs.Nunchuk.C, controller.C); holder.SetValue(Inputs.Nunchuk.Z, controller.Z);
        holder.SetValue(Inputs.Nunchuk.RIGHT, controller.joystick.X > 0 ? controller.joystick.X : 0f);
        holder.SetValue(Inputs.Nunchuk.LEFT, controller.joystick.X < 0 ? -controller.joystick.X : 0f);
        holder.SetValue(Inputs.Nunchuk.UP, controller.joystick.Y > 0 ? controller.joystick.Y : 0f);
        holder.SetValue(Inputs.Nunchuk.DOWN, controller.joystick.Y < 0 ? -controller.joystick.Y : 0f);
        holder.SetValue(Inputs.Nunchuk.TILT_RIGHT, controller.accelerometer.X > 0 ? controller.accelerometer.X : 0f);
        holder.SetValue(Inputs.Nunchuk.TILT_LEFT, controller.accelerometer.X < 0 ? -controller.accelerometer.X : 0f);
        holder.SetValue(Inputs.Nunchuk.TILT_UP, controller.accelerometer.Y > 0 ? controller.accelerometer.Y : 0f);
        holder.SetValue(Inputs.Nunchuk.TILT_DOWN, controller.accelerometer.Y < 0 ? -controller.accelerometer.Y : 0f);
        holder.SetValue(Inputs.Nunchuk.ACC_SHAKE_X, controller.accelerometer.X > 1.15f);
        holder.SetValue(Inputs.Nunchuk.ACC_SHAKE_Y, controller.accelerometer.Y > 1.15f);
        holder.SetValue(Inputs.Nunchuk.ACC_SHAKE_Z, controller.accelerometer.Z > 1.15f);
    }

    public void ApplyClassic(Holder holder, ClassicController controller)
    {
        holder.SetValue(Inputs.ClassicController.A, controller.A); holder.SetValue(Inputs.ClassicController.B, controller.B);
        holder.SetValue(Inputs.ClassicController.X, controller.X); holder.SetValue(Inputs.ClassicController.Y, controller.Y);
        holder.SetValue(Inputs.ClassicController.UP, controller.Up); holder.SetValue(Inputs.ClassicController.DOWN, controller.Down);
        holder.SetValue(Inputs.ClassicController.LEFT, controller.Left); holder.SetValue(Inputs.ClassicController.RIGHT, controller.Right);
        holder.SetValue(Inputs.ClassicController.L, controller.L.value > 0); holder.SetValue(Inputs.ClassicController.R, controller.R.value > 0);
        holder.SetValue(Inputs.ClassicController.ZL, controller.ZL); holder.SetValue(Inputs.ClassicController.ZR, controller.ZR);
        holder.SetValue(Inputs.ClassicController.START, controller.Start); holder.SetValue(Inputs.ClassicController.SELECT, controller.Select);
        holder.SetValue(Inputs.ClassicController.HOME, controller.Home);
        holder.SetValue(Inputs.ClassicController.LFULL, controller.LFull); holder.SetValue(Inputs.ClassicController.RFULL, controller.RFull);
        holder.SetValue(Inputs.ClassicController.LT, controller.L.value > 0.1f ? controller.L.value : 0f);
        holder.SetValue(Inputs.ClassicController.RT, controller.R.value > 0.1f ? controller.R.value : 0f);
        holder.SetValue(Inputs.ClassicController.LRIGHT, controller.LJoy.X > 0 ? controller.LJoy.X : 0f);
        holder.SetValue(Inputs.ClassicController.LLEFT, controller.LJoy.X < 0 ? -controller.LJoy.X : 0f);
        holder.SetValue(Inputs.ClassicController.LUP, controller.LJoy.Y > 0 ? controller.LJoy.Y : 0f);
        holder.SetValue(Inputs.ClassicController.LDOWN, controller.LJoy.Y < 0 ? -controller.LJoy.Y : 0f);
        holder.SetValue(Inputs.ClassicController.RRIGHT, controller.RJoy.X > 0 ? controller.RJoy.X : 0f);
        holder.SetValue(Inputs.ClassicController.RLEFT, controller.RJoy.X < 0 ? -controller.RJoy.X : 0f);
        holder.SetValue(Inputs.ClassicController.RUP, controller.RJoy.Y > 0 ? controller.RJoy.Y : 0f);
        holder.SetValue(Inputs.ClassicController.RDOWN, controller.RJoy.Y < 0 ? -controller.RJoy.Y : 0f);
    }

    public void ApplyClassicPro(Holder holder, ClassicControllerPro controller)
    {
        holder.SetValue(Inputs.ClassicControllerPro.A, controller.A); holder.SetValue(Inputs.ClassicControllerPro.B, controller.B);
        holder.SetValue(Inputs.ClassicControllerPro.X, controller.X); holder.SetValue(Inputs.ClassicControllerPro.Y, controller.Y);
        holder.SetValue(Inputs.ClassicControllerPro.UP, controller.Up); holder.SetValue(Inputs.ClassicControllerPro.DOWN, controller.Down);
        holder.SetValue(Inputs.ClassicControllerPro.LEFT, controller.Left); holder.SetValue(Inputs.ClassicControllerPro.RIGHT, controller.Right);
        holder.SetValue(Inputs.ClassicControllerPro.L, controller.L); holder.SetValue(Inputs.ClassicControllerPro.R, controller.R);
        holder.SetValue(Inputs.ClassicControllerPro.ZL, controller.ZL); holder.SetValue(Inputs.ClassicControllerPro.ZR, controller.ZR);
        holder.SetValue(Inputs.ClassicControllerPro.START, controller.Start); holder.SetValue(Inputs.ClassicControllerPro.SELECT, controller.Select);
        holder.SetValue(Inputs.ClassicControllerPro.HOME, controller.Home);
        holder.SetValue(Inputs.ClassicControllerPro.LRIGHT, controller.LJoy.X > 0 ? controller.LJoy.X : 0f);
        holder.SetValue(Inputs.ClassicControllerPro.LLEFT, controller.LJoy.X < 0 ? -controller.LJoy.X : 0f);
        holder.SetValue(Inputs.ClassicControllerPro.LUP, controller.LJoy.Y > 0 ? controller.LJoy.Y : 0f);
        holder.SetValue(Inputs.ClassicControllerPro.LDOWN, controller.LJoy.Y < 0 ? -controller.LJoy.Y : 0f);
        holder.SetValue(Inputs.ClassicControllerPro.RRIGHT, controller.RJoy.X > 0 ? controller.RJoy.X : 0f);
        holder.SetValue(Inputs.ClassicControllerPro.RLEFT, controller.RJoy.X < 0 ? -controller.RJoy.X : 0f);
        holder.SetValue(Inputs.ClassicControllerPro.RUP, controller.RJoy.Y > 0 ? controller.RJoy.Y : 0f);
        holder.SetValue(Inputs.ClassicControllerPro.RDOWN, controller.RJoy.Y < 0 ? -controller.RJoy.Y : 0f);
    }

    public void ApplyDrums(Holder holder, WiiDrums controller)
    {
        holder.SetValue(Inputs.WiiDrums.G, controller.G); holder.SetValue(Inputs.WiiDrums.R, controller.R);
        holder.SetValue(Inputs.WiiDrums.Y, controller.Y); holder.SetValue(Inputs.WiiDrums.B, controller.B);
        holder.SetValue(Inputs.WiiDrums.O, controller.O); holder.SetValue(Inputs.WiiDrums.BASS, controller.Bass);
        holder.SetValue(Inputs.WiiDrums.UP, controller.Up); holder.SetValue(Inputs.WiiDrums.DOWN, controller.Down);
        holder.SetValue(Inputs.WiiDrums.LEFT, controller.Left); holder.SetValue(Inputs.WiiDrums.RIGHT, controller.Right);
        holder.SetValue(Inputs.WiiDrums.START, controller.Start); holder.SetValue(Inputs.WiiDrums.SELECT, controller.Select);
    }
}
