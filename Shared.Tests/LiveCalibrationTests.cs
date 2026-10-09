using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using NintrollerLib;
using Xunit;
using FixtureStream = Shared.Tests.NintrollerTransportTests.FakeTransport;

namespace Shared.Tests;

public sealed class LiveCalibrationTests
{
    [Theory]
    [InlineData("wiimote")]
    [InlineData("nunchuk")]
    [InlineData("nunchuk-b")]
    [InlineData("classic")]
    [InlineData("classic-pro")]
    [InlineData("pro")]
    [InlineData("guitar")]
    [InlineData("drums")]
    public async Task LoadingStoredCalibrationImmediatelyUpdatesActiveStateAndSurvivesExtensionChanges(string extension)
    {
        using var stream = new FixtureStream();
        await using var controller = new Nintroller(stream);
        controller.SetCalibration(Calibrations.CalibrationPreset.Minimum);
        INintrollerState? active = null;
        int changes = 0;
        controller.StateUpdate += (_, args) => active = args.state;
        controller.ExtensionChange += (_, _) => changes++;
        controller.BeginReading();
        await Negotiate(stream, extension);
        await Send(stream, Packet("buttons-a"));
        Assert.NotNull(active);
        string previous = CalibrationSignature(active);
        var expected = new CalibrationStorage();
        expected.SetCalibrations(Calibrations.CalibrationPreset.Extra);

        controller.SetCalibration(expected.ToString());

        // Inspect the already published state before another report arrives. Loading
        // must update its calibration in place, without replacing its input state.
        Assert.NotEqual(previous, CalibrationSignature(active));
        AssertCalibration(expected, active);
        AssertNormalized(active);
        Assert.Equal(1, changes);
        Assert.Equal(expected.ToString(), controller.StoredCalibrations.ToString());

        await Negotiate(stream, extension == "wiimote" ? "nunchuk" : "wiimote");
        await Negotiate(stream, extension);
        await Send(stream, Packet("buttons-a"));
        AssertCalibration(expected, active!);
        Assert.Equal(3, changes);
    }

    [Fact]
    public async Task APartialCalibrationUpdatesTheRemoteWithoutChangingExtensionCalibrationOrInput()
    {
        using var stream = new FixtureStream();
        await using var controller = new Nintroller(stream);
        Nunchuk? published = null;
        INintrollerState? active = null;
        controller.StateUpdate += (_, args) => { active = args.state; published = (Nunchuk)args.state; };
        controller.BeginReading();
        await Negotiate(stream, "nunchuk");
        var report = Packet("buttons-a");
        report[0] = 0x35; // Buttons, accelerometer, and six extension bytes.
        report[3] = 150;
        report[6] = 130;
        report[7] = 140;
        await Send(stream, report);
        Assert.NotNull(published);
        var original = published.Value;
        var remote = Calibrations.Extras.WiimoteExtra;

        controller.SetCalibration(remote.GetCalibrationString());

        var updated = Assert.IsType<Nunchuk>(active);
        Assert.Equal(remote.GetCalibrationString(), updated.wiimote.GetCalibrationString());
        Assert.Equal(original.GetCalibrationString(), updated.GetCalibrationString());
        Assert.Equal(original.joystick.rawX, updated.joystick.rawX);
        Assert.Equal(original.joystick.rawY, updated.joystick.rawY);
        Assert.Equal(original.wiimote.accelerometer.rawX, updated.wiimote.accelerometer.rawX);
        Assert.Equal(original.wiimote.buttons.A, updated.wiimote.buttons.A);
        var normalized = updated.wiimote.accelerometer;
        normalized.Normalize();
        Assert.Equal(normalized.X, updated.wiimote.accelerometer.X);
        Assert.NotEqual(original.wiimote.accelerometer.X, updated.wiimote.accelerometer.X);
    }

    [Fact]
    public async Task CalibrationUsesForcedControllerTypeRatherThanTheReportedExtension()
    {
        using var stream = new FixtureStream();
        await using var controller = new Nintroller(stream);
        controller.ForceControllerType(ControllerType.ClassicControllerPro);
        INintrollerState? active = null;
        controller.StateUpdate += (_, args) => active = args.state;
        controller.BeginReading();
        await Negotiate(stream, "guitar");
        // The existing API applies the forced type on a repeated identifier reply.
        await Negotiate(stream, "guitar");
        await Send(stream, Packet("buttons-a"));
        Assert.Equal(ControllerType.ClassicControllerPro, controller.Type);
        var expected = new CalibrationStorage();
        expected.SetCalibrations(Calibrations.CalibrationPreset.Extra);

        controller.SetCalibration(expected.ToString());

        AssertCalibration(expected, active!);
    }

    [Fact]
    public void CalibrationCanBeLoadedBeforeIdentificationAndEmptyInputIsANoOp()
    {
        using var stream = new FixtureStream();
        using var controller = new Nintroller(stream);
        var expected = new CalibrationStorage();
        expected.SetCalibrations(Calibrations.CalibrationPreset.Extra);
        controller.SetCalibration(expected.ToString());
        controller.SetCalibration(string.Empty);
        Assert.Equal(expected.ToString(), controller.StoredCalibrations.ToString());
        Assert.Equal(ControllerType.Unknown, controller.Type);
    }

    [Fact]
    public void AnUnrelatedCalibrationDoesNotChangeTheRemotesCachedInput()
    {
        var remote = Calibrations.Defaults.WiimoteDefault;
        remote.accelerometer.X = 0.25f;
        remote.SetCalibration(Calibrations.Defaults.ProControllerDefault);
        Assert.Equal(0.25f, remote.accelerometer.X);
    }

    private static void AssertNormalized(INintrollerState state)
    {
        switch (state)
        {
            case Wiimote remote:
                AssertNormalized(remote.accelerometer);
                break;
            case Nunchuk nunchuk:
                AssertNormalized(nunchuk.wiimote.accelerometer);
                AssertNormalized(nunchuk.accelerometer);
                AssertNormalized(nunchuk.joystick);
                break;
            case ClassicController classic:
                AssertNormalized(classic.wiimote.accelerometer);
                AssertNormalized(classic.LJoy);
                AssertNormalized(classic.RJoy);
                AssertNormalized(classic.L);
                AssertNormalized(classic.R);
                break;
            case ClassicControllerPro classicPro:
                AssertNormalized(classicPro.wiimote.accelerometer);
                AssertNormalized(classicPro.LJoy);
                AssertNormalized(classicPro.RJoy);
                break;
            case ProController pro:
                AssertNormalized(pro.LJoy);
                AssertNormalized(pro.RJoy);
                break;
            case WiiGuitar guitar:
                AssertNormalized(guitar.wiimote.accelerometer);
                break;
            case WiiDrums drums:
                AssertNormalized(drums.wiimote.accelerometer);
                break;
        }
    }

    private static void AssertNormalized(Accelerometer value)
    {
        var expected = value;
        expected.Normalize();
        Assert.Equal(expected.X, value.X);
        Assert.Equal(expected.Y, value.Y);
        Assert.Equal(expected.Z, value.Z);
    }

    private static void AssertNormalized(Joystick value)
    {
        var expected = value;
        expected.Normalize();
        Assert.Equal(expected.X, value.X);
        Assert.Equal(expected.Y, value.Y);
    }

    private static void AssertNormalized(Trigger value)
    {
        var expected = value;
        expected.Normalize();
        Assert.Equal(expected.value, value.value);
    }

    private static string CalibrationSignature(INintrollerState state) => state switch
    {
        Nunchuk value => value.wiimote.GetCalibrationString() + value.GetCalibrationString(),
        ClassicController value => value.wiimote.GetCalibrationString() + value.GetCalibrationString(),
        ClassicControllerPro value => value.wiimote.GetCalibrationString() + value.GetCalibrationString(),
        WiiGuitar value => value.wiimote.GetCalibrationString(),
        WiiDrums value => value.wiimote.GetCalibrationString(),
        _ => state.GetCalibrationString()
    };

    private static void AssertCalibration(CalibrationStorage expected, INintrollerState state)
    {
        switch (state)
        {
            case Wiimote remote:
                Assert.Equal(expected.WiimoteCalibration.GetCalibrationString(), remote.GetCalibrationString());
                break;
            case Nunchuk nunchuk:
                Assert.Equal(expected.WiimoteCalibration.GetCalibrationString(), nunchuk.wiimote.GetCalibrationString());
                Assert.Equal(expected.NunchukCalibration.GetCalibrationString(), nunchuk.GetCalibrationString());
                break;
            case ClassicController classic:
                Assert.Equal(expected.WiimoteCalibration.GetCalibrationString(), classic.wiimote.GetCalibrationString());
                Assert.Equal(expected.ClassicCalibration.GetCalibrationString(), classic.GetCalibrationString());
                break;
            case ClassicControllerPro classicPro:
                Assert.Equal(expected.WiimoteCalibration.GetCalibrationString(), classicPro.wiimote.GetCalibrationString());
                Assert.Equal(expected.ClassicProCalibration.GetCalibrationString(), classicPro.GetCalibrationString());
                break;
            case ProController pro:
                Assert.Equal(expected.ProCalibration.GetCalibrationString(), pro.GetCalibrationString());
                break;
            case WiiGuitar guitar:
                Assert.Equal(expected.WiimoteCalibration.GetCalibrationString(), guitar.wiimote.GetCalibrationString());
                break;
            case WiiDrums drums:
                Assert.Equal(expected.WiimoteCalibration.GetCalibrationString(), drums.wiimote.GetCalibrationString());
                break;
            default:
                throw new InvalidOperationException("Expected an identified controller state.");
        }
    }

    private static byte[] Packet(string name)
    {
        using var fixtures = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "reports.json")));
        return Convert.FromHexString(fixtures.RootElement.GetProperty("reports").GetProperty(name).GetString()!);
    }

    private static async Task Send(FixtureStream stream, byte[] report)
    {
        int nextRead = stream.BeginReadCalls + 1;
        stream.CompleteRead(report);
        await stream.WaitForReadAsync(nextRead);
    }

    private static async Task Negotiate(FixtureStream stream, string extension)
    {
        await Send(stream, Packet("status"));
        await Send(stream, Packet("init"));
        await Send(stream, Packet(extension));
    }
}
