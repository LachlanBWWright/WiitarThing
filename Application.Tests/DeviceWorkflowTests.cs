using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using NintrollerLib;
using Shared;
using WiinUSoft;
using WiinUSoft.Services;
using WiinUSoft.ViewModels;
using WiinUSoft.VirtualOutput;
using Xunit;

namespace Application.Tests;

public sealed class DeviceWorkflowTests
{
    [Fact]
    public void MissingOutputDriverFailsConnectionAndStopsTransport()
    {
        using var stream = new FakeTransport();
        var controller = new Nintroller(stream, ControllerType.Guitar);
        var backend = new FakeBackend { FailConnect = true };
        using var session = CreateSession(controller, backend);
        controller.BeginReading();
        var failures = 0;
        session.ConnectionFailed += (_, _) => failures++;

        Assert.False(new DeviceConnectionService().TryConnectToXInput(session, 1));

        Assert.Equal(DeviceState.Discovered, session.ConnectionState);
        Assert.False(controller.Connected);
        Assert.True(backend.Disposed);
        Assert.Null(session.VirtualOutput.Backend);
        Assert.Equal(1, failures);
    }

    [Fact]
    public void TransportFailureDuringConnectRollsBackOutput()
    {
        using var stream = new FakeTransport { ThrowOnWrite = true };
        var backend = new FakeBackend();
        using var session = CreateSession(new Nintroller(stream, ControllerType.Guitar), backend);
        Assert.True(session.AssignToXInput(1).IsError);
        Assert.Equal(DeviceState.Discovered, session.ConnectionState);
        Assert.True(backend.Disposed);
        Assert.True(backend.Readback.Disposed);
        Assert.False(session.Controller.Connected);
    }

    [Fact]
    public void OutputWriteFailureIsReportedAndDisconnectsTheSession()
    {
        using var stream = new FakeTransport();
        var controller = new Nintroller(stream, ControllerType.Guitar);
        var backend = new FakeBackend { FailWrites = true };
        using var session = CreateSession(controller, backend);
        Assert.True(session.AssignToXInput(1).IsOk);
        VirtualControllerError? failure = null;
        session.ConnectionFailed += (_, args) => failure = args.Error;
        ControllerEvents.Publish(controller, ControllerType.Guitar, new WiiGuitar());
        Assert.NotNull(failure);
        Assert.Equal(VirtualControllerErrorKind.WriteFailed, failure.Kind);
        Assert.Equal(DeviceState.Discovered, session.ConnectionState);
        Assert.True(backend.Disposed);
    }

    [Fact]
    public void ExperimentalWriteErrorsStayInlineAndAreReportedOnceUntilRecovery()
    {
        using var stream = new FakeTransport();
        var controller = new Nintroller(stream, ControllerType.Guitar);
        var backend = new FakeBackend(VirtualOutputMode.HidMaestroExperimental) { FailWrites = true };
        var configuration = new FakeConfiguration();
        configuration.LoadDevice("device", ControllerType.Guitar).Properties.virtualOutputMode = backend.Mode;
        using var session = CreateSession(controller, backend, configuration);
        Assert.True(session.AssignToXInput(1).IsOk);
        var errors = 0;
        session.InlineOutputError += (_, error) => { if (!string.IsNullOrEmpty(error)) errors++; };
        ControllerEvents.Publish(controller, ControllerType.Guitar, new WiiGuitar());
        ControllerEvents.Publish(controller, ControllerType.Guitar, new WiiGuitar());
        Assert.Equal(1, errors);
        Assert.Equal(DeviceState.Connected_XInput, session.ConnectionState);
        backend.FailWrites = false;
        ControllerEvents.Publish(controller, ControllerType.Guitar, new WiiGuitar());
        backend.FailWrites = true;
        ControllerEvents.Publish(controller, ControllerType.Guitar, new WiiGuitar());
        Assert.Equal(2, errors);
    }

    [Fact]
    public async Task ConnectionLostIsRaisedAfterHolderAndStateAreCleanedUp()
    {
        using var stream = new FakeTransport();
        var holder = new FakeHolder();
        using var session = CreateSession(new Nintroller(stream, ControllerType.Wiimote), new FakeBackend(), holder: holder);
        Assert.True(session.AssignToXInput(1).IsOk);
        var losses = 0;
        session.ConnectionLost += (_, _) =>
        {
            losses++;
            Assert.Equal(DeviceState.Discovered, session.ConnectionState);
            Assert.Equal(1, holder.Closes);
            Assert.Null(session.CurrentHolder);
        };
        stream.CompleteRead(Array.Empty<byte>());
        await session.Controller.ReadingCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, losses);
    }

    [Fact]
    public void DisposeClosesNonGuitarHolderExactlyOnce()
    {
        using var stream = new FakeTransport();
        var controller = new Nintroller(stream, ControllerType.Wiimote);
        var holder = new FakeHolder();
        var session = CreateSession(controller, new FakeBackend(), holder: holder);
        Assert.True(session.AssignToXInput(1).IsOk);
        session.Dispose();
        session.Dispose();
        ControllerEvents.Publish(controller, ControllerType.Wiimote, new Wiimote());
        Assert.Equal(1, holder.Closes);
        Assert.Equal(0, holder.Updates);
        Assert.True(stream.Closed);
        Assert.True(session.AssignToXInput(1).IsError);
    }

    [Fact]
    public async Task DisposeWaitsForActiveOutputWorkAndRejectsFurtherReports()
    {
        using var stream = new FakeTransport();
        var controller = new Nintroller(stream, ControllerType.Guitar);
        var backend = new FakeBackend();
        using var session = CreateSession(controller, backend);
        Assert.True(session.AssignToXInput(1).IsOk);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        backend.DuringUpdate = () =>
        {
            entered.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
        };
        var report = Task.Run(() => ControllerEvents.Publish(controller, ControllerType.Guitar, new WiiGuitar()));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            var shutdown = Task.Run(session.Dispose);
            Assert.False(shutdown.IsCompleted);
            release.Set();
            await Task.WhenAll(report, shutdown).WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { release.Set(); }
        ControllerEvents.Publish(controller, ControllerType.Guitar, new WiiGuitar());
        Assert.True(backend.Disposed);
        Assert.Equal(1, backend.Writes);
    }

    [Fact]
    public void ExtensionChangeReleasesPreviousHolderBeforeConnectingNewOutput()
    {
        using var stream = new FakeTransport();
        var controller = new Nintroller(stream, ControllerType.Wiimote);
        var holder = new FakeHolder();
        var backend = new FakeBackend();
        using var session = CreateSession(controller, backend, holder: holder);
        Assert.True(session.AssignToXInput(2).IsOk);
        ControllerEvents.ChangeExtension(controller, ControllerType.Guitar);
        Assert.Equal(1, holder.Closes);
        Assert.Null(session.CurrentHolder);
        Assert.Equal(2, backend.Slot);
        Assert.Equal(DeviceState.Connected_XInput, session.ConnectionState);
    }

    [Fact]
    public async Task BackgroundStateChangesNotifyBindingsOnlyThroughTheUiDispatcher()
    {
        using var stream = new FakeTransport();
        var controller = new Nintroller(stream, ControllerType.Guitar);
        var configuration = new FakeConfiguration();
        var session = CreateSession(controller, new FakeBackend(), configuration);
        var dispatcher = new QueuedDispatcher();
        using var model = new DeviceViewModel(session, configuration, dispatcher);
        Assert.True(session.AssignToXInput(1).IsOk);
        dispatcher.Drain();
        var threads = new List<int>();
        model.PropertyChanged += (_, _) => threads.Add(Environment.CurrentManagedThreadId);
        model.DisconnectCommand.CanExecuteChanged += (_, _) => threads.Add(Environment.CurrentManagedThreadId);
        await Task.Run(() => stream.CompleteRead(Array.Empty<byte>()));
        await session.Controller.ReadingCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(threads);
        var ownerThread = Environment.CurrentManagedThreadId;
        dispatcher.Drain();
        Assert.NotEmpty(threads);
        Assert.All(threads, thread => Assert.Equal(ownerThread, thread));
        Assert.False(model.IsConnected);
    }

    [Fact]
    public async Task RemovedDeviceCannotBeReinsertedByQueuedStateNotifications()
    {
        using var stream = new FakeTransport();
        var controller = new Nintroller(stream, ControllerType.Guitar);
        var configuration = new FakeConfiguration();
        var session = CreateSession(controller, new FakeBackend(), configuration);
        var dispatcher = new QueuedDispatcher();
        using var device = new DeviceViewModel(session, configuration, dispatcher);
        var main = new MainWindowViewModel(new FakePreferences(),
            new FakeDiscovery(new DeviceInfo { DevicePath = "device", Type = ControllerType.Guitar }),
            new DeviceConnectionService(), _ => device, new FakeOperations(), dispatcher);
        main.RefreshDevices();
        Assert.True(session.AssignToXInput(1).IsOk);
        dispatcher.Drain();
        Assert.Single(main.ConnectedDevices);
        await Task.Run(() => stream.CompleteRead(Array.Empty<byte>()));
        await session.Controller.ReadingCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        dispatcher.Drain();
        Assert.Empty(main.AvailableDevices);
        Assert.Empty(main.ConnectedDevices);
        Assert.True(stream.Closed);
    }

    [Fact]
    public void AutoConnectKeepsThePlayerSlotWhenTheFirstBackendFails()
    {
        var configuration = new FakeConfiguration();
        var backends = new[] { new FakeBackend { FailConnect = true }, new FakeBackend() };
        var devices = new List<DeviceViewModel>();
        var main = new MainWindowViewModel(new FakePreferences(), new FakeDiscovery(
            new DeviceInfo { DevicePath = "first", Type = ControllerType.Guitar },
            new DeviceInfo { DevicePath = "second", Type = ControllerType.Guitar }),
            new DeviceConnectionService(), info =>
            {
                var controller = new Nintroller(new FakeTransport(), info.Type);
                var session = CreateSession(controller, backends[devices.Count], configuration, path: info.DevicePath);
                session.AutoConnectSlot = 1;
                controller.BeginReading();
                var device = new DeviceViewModel(session, configuration);
                devices.Add(device);
                return device;
            }, new FakeOperations());
        try
        {
            main.RefreshDevices();
            Assert.Equal(1, backends[0].Slot);
            Assert.Equal(1, backends[1].Slot);
            Assert.Single(main.ConnectedDevices);
            Assert.Single(main.AvailableDevices);
        }
        finally { foreach (var device in devices) device.Dispose(); }
    }

    [Fact]
    public void OutputAndPreviewSelectionAffectOnlyTheSelectedDevice()
    {
        var configuration = new FakeConfiguration { VirtualOutputMode = VirtualOutputMode.VJoyExperimental };
        var first = CreateSession(new Nintroller(new FakeTransport(), ControllerType.Guitar), new FakeBackend(), configuration, path: "first");
        var second = CreateSession(new Nintroller(new FakeTransport(), ControllerType.Guitar), new FakeBackend(), configuration, path: "second");
        using var firstModel = new DeviceViewModel(first, configuration);
        using var secondModel = new DeviceViewModel(second, configuration);
        Assert.Equal((int)VirtualOutputMode.VJoyExperimental, firstModel.VirtualOutputModeIndex);
        firstModel.VirtualOutputModeIndex = (int)VirtualOutputMode.ScpXbox360;
        firstModel.GuitarPreviewModeIndex = (int)GuitarPreviewMode.VirtualOutput;
        firstModel.VJoyDeviceId = 2;
        Assert.Equal(2, firstModel.VJoyDeviceId);
        Assert.Equal(1, secondModel.VJoyDeviceId);
        Assert.Equal((int)VirtualOutputMode.VJoyExperimental, secondModel.VirtualOutputModeIndex);
        Assert.Equal((int)GuitarPreviewMode.WiiController, secondModel.GuitarPreviewModeIndex);
        Assert.Equal(VirtualOutputMode.VJoyExperimental, configuration.VirtualOutputMode);
        Assert.Equal(GuitarPreviewMode.WiiController, configuration.GuitarPreviewMode);
    }

    [Fact]
    public void ReconnectUsesDeviceSettingAndCleansUpWhenReplacementFails()
    {
        using var stream = new FakeTransport();
        var configuration = new FakeConfiguration();
        var initial = new FakeBackend();
        var replacement = new FakeBackend(VirtualOutputMode.VJoyExperimental) { FailConnect = true };
        var modes = new List<VirtualOutputMode>();
        using var session = new DeviceSession(new Nintroller(stream, ControllerType.Guitar), "device", configuration, backendFactory: mode =>
        {
            modes.Add(mode);
            return modes.Count == 1 ? initial : replacement;
        });
        session.Refresh();
        Assert.True(session.AssignToXInput(1).IsOk);
        configuration.GetDevicePreference("device")!.virtualOutputMode = VirtualOutputMode.VJoyExperimental;
        Assert.True(session.ReconnectVirtualOutput().IsError);
        Assert.Equal(new[] { VirtualOutputMode.ScpXbox360, VirtualOutputMode.VJoyExperimental }, modes);
        Assert.True(initial.Disposed);
        Assert.True(initial.Readback.Disposed);
        Assert.True(replacement.Disposed);
        Assert.Equal(DeviceState.Discovered, session.ConnectionState);
    }

    [Fact]
    public void DeviceSettingsRoundTripAndLegacyXmlKeepsFallbackDefaults()
    {
        var serializer = new XmlSerializer(typeof(Property));
        using var legacy = new StringReader("<Property><hid>old-device</hid></Property>");
        var old = (Property)serializer.Deserialize(legacy)!;
        Assert.Null(old.virtualOutputMode);
        Assert.Null(old.guitarPreviewMode);
        Assert.Null(old.vJoyDeviceId);
        var preferences = new Property("new-device")
        {
            virtualOutputMode = VirtualOutputMode.HidMaestroExperimental,
            guitarPreviewMode = GuitarPreviewMode.VirtualOutput,
            vJoyDeviceId = 2
        };
        using var writer = new StringWriter();
        serializer.Serialize(writer, preferences);
        using var reader = new StringReader(writer.ToString());
        var copy = new Property((Property)serializer.Deserialize(reader)!);
        Assert.Equal(preferences.virtualOutputMode, copy.virtualOutputMode);
        Assert.Equal(preferences.guitarPreviewMode, copy.guitarPreviewMode);
        Assert.Equal(preferences.vJoyDeviceId, copy.vJoyDeviceId);
    }

    [Fact]
    public void GuitarMappingPreservesButtonsAndClampsAxes()
    {
        var guitar = new WiiGuitar { G = true, O = true, Down = true, Plus = true, WhammyHigh = 2f, TiltLow = -2f };
        var output = new ControllerInputMapper().ApplyGuitar(null, guitar);
        Assert.True(output.Green);
        Assert.True(output.Orange);
        Assert.True(output.StrumDown);
        Assert.True(output.Start);
        Assert.False(output.Red);
        Assert.Equal(1f, output.Whammy);
        Assert.Equal(-1f, output.Tilt);
    }

    [Fact]
    public void OutputFailureRemainsVisibleAfterTheDeviceMovesToADisconnectedRow()
    {
        using var stream = new FakeTransport();
        var configuration = new FakeConfiguration();
        var controller = new Nintroller(stream, ControllerType.Guitar);
        var session = CreateSession(controller, new FakeBackend { FailWrites = true }, configuration);
        var dispatcher = new QueuedDispatcher();
        using var model = new DeviceViewModel(session, configuration, dispatcher);
        Assert.True(session.AssignToXInput(1).IsOk);
        ControllerEvents.Publish(controller, ControllerType.Guitar, new WiiGuitar());
        dispatcher.Drain();
        Assert.False(model.IsConnected);
        Assert.Contains("Synthetic output failure", model.OutputError);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, model.OutputErrorVisibility);
    }

    [Fact]
    public void InlineOutputErrorClearsWhenTheExperimentalBackendRecovers()
    {
        using var stream = new FakeTransport();
        var configuration = new FakeConfiguration();
        var controller = new Nintroller(stream, ControllerType.Guitar);
        var backend = new FakeBackend(VirtualOutputMode.HidMaestroExperimental) { FailWrites = true };
        configuration.LoadDevice("device", ControllerType.Guitar).Properties.virtualOutputMode = backend.Mode;
        var session = CreateSession(controller, backend, configuration);
        using var model = new DeviceViewModel(session, configuration);
        Assert.True(session.AssignToXInput(1).IsOk);
        ControllerEvents.Publish(controller, ControllerType.Guitar, new WiiGuitar());
        Assert.NotEmpty(model.OutputError);
        backend.FailWrites = false;
        ControllerEvents.Publish(controller, ControllerType.Guitar, new WiiGuitar());
        Assert.Empty(model.OutputError);
        Assert.True(model.IsConnected);
    }

    [Theory]
    [InlineData(ControllerType.Wiimote)]
    [InlineData(ControllerType.Nunchuk)]
    [InlineData(ControllerType.NunchukB)]
    [InlineData(ControllerType.ClassicController)]
    [InlineData(ControllerType.ClassicControllerPro)]
    [InlineData(ControllerType.ProController)]
    public void CollectedCalibrationValuesAreStoredRatherThanDiscarded(ControllerType type)
    {
        var workflow = new CalibrationWorkflow();
        workflow.StoreCalibration(type, new CalibrationWorkflow.Values
        {
            G1C = 123, G1Min = 80, G1Max = 210, G1Dead = 4
        });
        var stored = workflow.Calibrations;
        var center = type switch
        {
            ControllerType.Wiimote => stored.WiimoteCalibration.accelerometer.centerX,
            ControllerType.Nunchuk => stored.NunchukCalibration.accelerometer.centerX,
            ControllerType.NunchukB => stored.NunchukCalibration.joystick.centerX,
            ControllerType.ClassicController => stored.ClassicCalibration.LJoy.centerX,
            ControllerType.ClassicControllerPro => stored.ClassicProCalibration.LJoy.centerX,
            _ => stored.ProCalibration.LJoy.centerX
        };
        Assert.Equal(123, center);
        Assert.Contains(type, workflow.CalibratedTypes);
    }

    [Fact]
    public async Task DisposeCancelsIdentifyBeforeClosingTheTransport()
    {
        using var stream = new FakeTransport();
        var session = CreateSession(new Nintroller(stream, ControllerType.Guitar), new FakeBackend());
        var identify = session.IdentifyAsync(false);
        session.Dispose();
        var writesAtShutdown = stream.Writes;
        await identify.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(stream.Closed);
        Assert.Equal(writesAtShutdown, stream.Writes);
    }

    [Fact]
    public async Task ConnectingDuringIdentifyDoesNotLetTheOldAnimationStopTheNewConnection()
    {
        using var stream = new FakeTransport();
        using var session = CreateSession(new Nintroller(stream, ControllerType.Guitar), new FakeBackend());
        var identify = session.IdentifyAsync(false);
        Assert.True(session.AssignToXInput(1).IsOk);
        await identify.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(session.Controller.Connected);
        Assert.Equal(DeviceState.Connected_XInput, session.ConnectionState);
    }

    [Fact]
    public async Task ShutdownWaitsForActivePollingAndPreventsUpdatesAfterHolderCloses()
    {
        using var stream = new FakeTransport();
        var holder = new FakeHolder();
        using var session = CreateSession(new Nintroller(stream, ControllerType.Wiimote), new FakeBackend(), holder: holder);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        holder.DuringUpdate = () => { entered.Set(); release.Wait(TimeSpan.FromSeconds(5)); };
        Assert.True(session.AssignToXInput(1).IsOk);
        try
        {
            Assert.True(await Task.Run(() => entered.Wait(TimeSpan.FromSeconds(5))));
            var shutdown = Task.Run(session.Dispose);
            Assert.NotSame(shutdown, await Task.WhenAny(shutdown, Task.Delay(100)));
            release.Set();
            await shutdown.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(100); // Let callbacks already queued by the old timer execute.
        }
        finally { release.Set(); }
        Assert.Equal(1, holder.Closes);
        Assert.Equal(0, holder.UpdatesAfterClose);
    }

    [Fact]
    public void MainWindowShutdownDisposesAvailableDevicesAndRejectsQueuedUpdates()
    {
        using var stream = new FakeTransport();
        var configuration = new FakeConfiguration();
        var session = CreateSession(new Nintroller(stream, ControllerType.Guitar), new FakeBackend(), configuration);
        var dispatcher = new QueuedDispatcher();
        using var device = new DeviceViewModel(session, configuration, dispatcher);
        var main = new MainWindowViewModel(new FakePreferences(),
            new FakeDiscovery(new DeviceInfo { DevicePath = "device", Type = ControllerType.Guitar }),
            new DeviceConnectionService(), _ => device, new FakeOperations(), dispatcher);
        main.RefreshDevices();
        Assert.True(session.AssignToXInput(1).IsOk); // Leaves a collection update queued.
        main.Dispose();
        dispatcher.Drain();
        main.RefreshDevices();
        Assert.True(stream.Closed);
        Assert.Empty(main.AvailableDevices);
        Assert.Empty(main.ConnectedDevices);
    }

    private static DeviceSession CreateSession(Nintroller controller, FakeBackend backend,
        FakeConfiguration? configuration = null, FakeHolder? holder = null, string path = "device")
    {
        var session = new DeviceSession(controller, path, configuration ?? new FakeConfiguration(),
            backendFactory: _ => backend,
            connectHolder: (_, _, _) => Result<WiinUSoft.Holders.Holder, VirtualControllerError>.Ok(holder ?? new FakeHolder()));
        session.Refresh();
        return session;
    }
}
