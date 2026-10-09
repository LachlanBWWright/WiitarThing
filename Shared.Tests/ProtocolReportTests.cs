using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using NintrollerLib;
using Xunit;
using FixtureStream = Shared.Tests.NintrollerTransportTests.FakeTransport;

namespace Shared.Tests;

public sealed class ProtocolReportTests
{
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

    [Theory]
    [InlineData("wiimote", ControllerType.Wiimote)]
    [InlineData("nunchuk", ControllerType.Nunchuk)]
    [InlineData("nunchuk-b", ControllerType.NunchukB)]
    [InlineData("classic", ControllerType.ClassicController)]
    [InlineData("classic-pro", ControllerType.ClassicControllerPro)]
    [InlineData("pro", ControllerType.ProController)]
    [InlineData("guitar", ControllerType.Guitar)]
    [InlineData("drums", ControllerType.Drums)]
    public async Task ExtensionRepliesSelectTheCorrectState(string fixture, ControllerType expected)
    {
        using var stream = new FixtureStream();
        await using var controller = new Nintroller(stream);
        int changes = 0;
        controller.ExtensionChange += (_, args) => { Assert.Equal(expected, args.controllerType); changes++; };
        controller.BeginReading();
        await Negotiate(stream, fixture);
        Assert.Equal(expected, controller.Type);
        Assert.Equal(1, changes);
        await Negotiate(stream, "detached");
        Assert.Equal(expected, controller.Type); // Partial insertion retries status without changing identity.
        Assert.Equal(1, changes);
        // Repeated identification does not replace an already identified state.
        await Negotiate(stream, fixture);
        Assert.Equal(1, changes);
        controller.StopReading();
        stream.CompleteRead(Array.Empty<byte>());
    }

    [Theory]
    [InlineData("short-id")]
    [InlineData("id-error")]
    public async Task InvalidIdentifiersAreIgnoredAndAValidReplyCanRecover(string fixture)
    {
        using var stream = new FixtureStream();
        await using var controller = new Nintroller(stream);
        controller.BeginReading();
        await Send(stream, Packet("status"));
        await Send(stream, Packet("init"));
        await Send(stream, Packet(fixture));
        Assert.Equal(ControllerType.Unknown, controller.Type);
        await Send(stream, Packet("guitar"));
        Assert.Equal(ControllerType.Guitar, controller.Type);
        controller.StopReading();
        stream.CompleteRead(Array.Empty<byte>());
    }

    [Fact]
    public async Task FailedInitializationDoesNotAdvanceNegotiation()
    {
        using var stream = new FixtureStream();
        await using var controller = new Nintroller(stream);
        controller.BeginReading();
        await Send(stream, Packet("status"));
        await Send(stream, Packet("init-error"));
        await Send(stream, Packet("init"));
        await Send(stream, Packet("drums"));
        Assert.Equal(ControllerType.Drums, controller.Type);
        controller.StopReading();
        stream.CompleteRead(Array.Empty<byte>());
    }

    [Fact]
    public async Task UnknownExtensionsDoNotPublishThePreviousControllerState()
    {
        using var stream = new FixtureStream();
        await using var controller = new Nintroller(stream);
        controller.BeginReading();
        await Negotiate(stream, "wiimote");
        int updates = 0;
        controller.StateUpdate += (_, _) => updates++;
        await Send(stream, Packet("buttons-a"));
        Assert.Equal(1, updates);
        await Negotiate(stream, "unknown-extension");
        await Send(stream, Packet("buttons-a"));
        Assert.Equal(1, updates);
        await Negotiate(stream, "wiimote");
        await Send(stream, Packet("buttons-a"));
        Assert.Equal(2, updates);
        controller.StopReading();
        stream.CompleteRead(Array.Empty<byte>());
    }

    [Theory]
    [InlineData("truncated")]
    [InlineData("unknown-report")]
    public async Task MalformedReportsDoNotPublishStateAndTheNextValidPacketWorks(string fixture)
    {
        using var stream = new FixtureStream();
        await using var controller = new Nintroller(stream);
        controller.BeginReading();
        await Negotiate(stream, "wiimote");
        int updates = 0;
        controller.StateUpdate += (_, args) => { Assert.True(Assert.IsType<Wiimote>(args.state).buttons.A); updates++; };
        await Send(stream, Packet(fixture));
        Assert.Equal(0, updates);
        await Send(stream, Packet("buttons-a"));
        Assert.Equal(1, updates);
        controller.StopReading();
        stream.CompleteRead(Array.Empty<byte>());
    }

    [Fact]
    public async Task LowBatteryNotificationIsRaisedOnceUntilRecovery()
    {
        using var stream = new FixtureStream();
        await using var controller = new Nintroller(stream);
        controller.BeginReading();
        int warnings = 0;
        controller.LowBattery += (_, _) => warnings++;
        await Send(stream, Packet("status-low"));
        await Send(stream, Packet("status-low"));
        Assert.Equal(1, warnings);
        Assert.Equal(BatteryStatus.VeryLow, controller.BatteryLevel);
        await Send(stream, Packet("status"));
        Assert.Equal(BatteryStatus.VeryHigh, controller.BatteryLevel);
        await Send(stream, Packet("status-low"));
        Assert.Equal(2, warnings);
        controller.StopReading();
        stream.CompleteRead(Array.Empty<byte>());
    }

    [Theory]
    [InlineData(0x24, 0)] // Green
    [InlineData(0x32, 1)] // Red
    [InlineData(0x22, 2)] // Yellow
    [InlineData(0x1E, 3)] // Blue
    [InlineData(0x1C, 4)] // Orange
    [InlineData(0x36, 5)] // Bass
    public void DrumHitsDecodeAtEverySupportedExtensionOffset(byte hit, int expectedPad)
    {
        foreach (var (report, offset) in new[] { (0x32, 3), (0x34, 3), (0x35, 6), (0x36, 13), (0x37, 16), (0x3D, 1) })
        {
            var bytes = new byte[22];
            bytes[0] = (byte)report;
            bytes[offset + 2] = hit;
            bytes[offset + 4] = bytes[offset + 5] = 0xFF;
            var drums = new WiiDrums(new Wiimote());
            drums.Update(bytes);
            var pads = new[] { drums.G, drums.R, drums.Y, drums.B, drums.O, drums.Bass };
            for (int index = 0; index < pads.Length; index++)
                Assert.Equal(index == expectedPad, pads[index]);
            bytes[offset + 2] = 0; // Idle packet releases the hit.
            drums.Update(bytes);
            Assert.False(drums.G || drums.R || drums.Y || drums.B || drums.O || drums.Bass);
        }
    }

    [Theory]
    [InlineData("wiimote")]
    [InlineData("nunchuk")]
    [InlineData("nunchuk-b")]
    [InlineData("classic")]
    [InlineData("classic-pro")]
    [InlineData("pro")]
    public async Task NegotiationAppliesStoredCalibrationAfterExtensionChanges(string fixture)
    {
        using var stream = new FixtureStream();
        await using var controller = new Nintroller(stream);
        controller.SetCalibration(Calibrations.CalibrationPreset.Extra);
        var stored = new CalibrationStorage(controller.StoredCalibrations.ToString());
        INintrollerState? state = null;
        controller.StateUpdate += (_, args) => state = args.state;
        controller.BeginReading();
        await Negotiate(stream, fixture);
        await Send(stream, Packet("buttons-a"));
        var expected = fixture switch
        {
            "wiimote" => stored.WiimoteCalibration.accelerometer.deadX,
            "nunchuk" or "nunchuk-b" => stored.NunchukCalibration.joystick.deadX,
            "classic" => stored.ClassicCalibration.LJoy.deadX,
            "classic-pro" => stored.ClassicProCalibration.LJoy.deadX,
            _ => stored.ProCalibration.LJoy.deadX
        };
        static int DeadZone(INintrollerState? input) => input switch
        {
            Wiimote remote => remote.accelerometer.deadX,
            Nunchuk nunchuk => nunchuk.joystick.deadX,
            ClassicController classic => classic.LJoy.deadX,
            ClassicControllerPro classic => classic.LJoy.deadX,
            ProController pro => pro.LJoy.deadX,
            _ => throw new InvalidOperationException("Expected a calibrated controller state.")
        };
        Assert.Equal(expected, DeadZone(state));
        await Negotiate(stream, fixture == "wiimote" ? "guitar" : "wiimote");
        await Negotiate(stream, fixture);
        await Send(stream, Packet("buttons-a"));
        Assert.Equal(expected, DeadZone(state));
        controller.StopReading();
        stream.CompleteRead(Array.Empty<byte>());
    }
}
