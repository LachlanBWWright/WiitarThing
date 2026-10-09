using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NintrollerLib;
using Shared;
using WiinUSoft;
using WiinUSoft.Holders;
using WiinUSoft.Services;
using WiinUSoft.VirtualOutput;
using Inputs = WiinUSoft.Inputs;

namespace Application.Tests;

internal sealed class FakeConfiguration : IDeviceConfigurationService
{
    private readonly Dictionary<string, Property> _devices = new();
    public VirtualOutputMode VirtualOutputMode { get; set; }
    public GuitarPreviewMode GuitarPreviewMode { get; set; }
    public int VJoyDeviceId => 1;
    public Profile? DefaultProfile => null;
    public int Saves { get; private set; }
    public DevicePreferencesSnapshot LoadDevice(string path, ControllerType type)
    {
        if (!_devices.TryGetValue(path, out var properties))
            _devices[path] = properties = new Property(path);
        return new(properties, type.ToString(), false, null);
    }
    public Property? GetDevicePreference(string path) => _devices.GetValueOrDefault(path);
    public void AddDevicePreference(Property property) => _devices[property.hid] = property;
    public void UpdateDeviceIcon(string path, string iconKey) => _devices[path].lastIcon = iconKey;
    public Result<Unit, PreferencesError> Save()
    {
        Saves++;
        return Result<Unit, PreferencesError>.Ok(Unit.Value);
    }
}

internal sealed class FakeTransport : Stream
{
    private PendingRead? _pending;
    public bool ThrowOnWrite { get; set; }
    public bool Closed { get; private set; }
    public int Writes { get; private set; }
    public int BeginReadCalls { get; private set; }
    public override bool CanRead => !Closed;
    public override bool CanWrite => !Closed;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => 0; set => throw new NotSupportedException(); }
    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        ObjectDisposedException.ThrowIf(Closed, this);
        if (_pending is not null)
            throw new InvalidOperationException("Overlapping native reads.");
        BeginReadCalls++;
        _pending = new PendingRead(buffer, callback, state);
        return _pending;
    }
        // Deliberately ignores cancellation to exercise stale completion handling.
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var bytes = new byte[buffer.Length];
            var completion = new TaskCompletionSource<int>();
            BeginRead(bytes, 0, bytes.Length, result =>
            {
                try
                {
                    int count = EndRead(result);
                    bytes.AsMemory(0, count).CopyTo(buffer);
                    completion.SetResult(count);
                }
                catch (IOException ex) { completion.SetException(ex); }
            }, null);
            return new ValueTask<int>(completion.Task);
        }
    public override int EndRead(IAsyncResult result) => ((PendingRead)result).BytesRead;
    public void CompleteRead(byte[] report)
    {
        var pending = _pending ?? throw new InvalidOperationException("No pending native read.");
        _pending = null;
        report.CopyTo(pending.Buffer, 0);
        pending.BytesRead = report.Length;
        pending.Completed.Set();
        pending.Callback?.Invoke(pending);
    }
    public override void Write(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(Closed, this);
        Writes++;
        if (ThrowOnWrite)
            throw new IOException("Synthetic write failure.");
    }
    protected override void Dispose(bool disposing)
    {
        if (!Closed && _pending is not null)
        {
            Closed = true;
            CompleteRead(Array.Empty<byte>());
        }
        Closed = true;
        base.Dispose(disposing);
    }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    private sealed class PendingRead(byte[] buffer, AsyncCallback? callback, object? state) : IAsyncResult
    {
        public byte[] Buffer { get; } = buffer;
        public AsyncCallback? Callback { get; } = callback;
        public int BytesRead { get; set; }
        public ManualResetEvent Completed { get; } = new(false);
        public object? AsyncState => state;
        public WaitHandle AsyncWaitHandle => Completed;
        public bool CompletedSynchronously => false;
        public bool IsCompleted => Completed.WaitOne(0);
    }
}

internal sealed class FakeBackend(VirtualOutputMode mode = VirtualOutputMode.ScpXbox360) : IVirtualControllerBackend
{
    public string DisplayName => "Fake output";
    public VirtualOutputMode Mode => mode;
    public bool FailConnect { get; set; }
    public bool FailWrites { get; set; }
    public int Slot { get; private set; }
    public int Writes { get; private set; }
    public int Disconnects { get; private set; }
    public bool Disposed { get; private set; }
    public ControllerOutputState? LastOutput { get; private set; }
    public Action? DuringUpdate { get; set; }
    public FakeReadback Readback { get; } = new();
    public Result<Unit, VirtualControllerError> Connect(int slotOrDeviceId, ControllerType sourceType)
    {
        Slot = slotOrDeviceId;
        return FailConnect ? Result<Unit, VirtualControllerError>.Err(VirtualControllerError.DriverNotReady("Synthetic missing driver."))
            : Result<Unit, VirtualControllerError>.Ok(Unit.Value);
    }
    public Result<Unit, VirtualControllerError> Update(ControllerOutputState state)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        DuringUpdate?.Invoke();
        Writes++;
        LastOutput = state;
        return FailWrites ? Result<Unit, VirtualControllerError>.Err(VirtualControllerError.WriteFailed("Synthetic output failure."))
            : Result<Unit, VirtualControllerError>.Ok(Unit.Value);
    }
    public Result<Unit, VirtualControllerError> Disconnect()
    {
        Disconnects++;
        return Result<Unit, VirtualControllerError>.Ok(Unit.Value);
    }
    public void Dispose() => Disposed = true;
    public Result<VirtualControllerIdentity, VirtualControllerError> GetIdentity()
        => Result<VirtualControllerIdentity, VirtualControllerError>.Ok(new(DisplayName, XInputSlot: Slot));
    public Result<IVirtualControllerReadback, VirtualControllerError> CreateReadback()
        => Result<IVirtualControllerReadback, VirtualControllerError>.Ok(Readback);
}

internal sealed class FakeReadback : IVirtualControllerReadback
{
    public string DisplayName => "Fake readback";
    public bool Disposed { get; private set; }
    public Result<Unit, VirtualControllerError> Attach(VirtualControllerIdentity identity)
        => Result<Unit, VirtualControllerError>.Ok(Unit.Value);
    public Result<ControllerOutputState, VirtualControllerError> ReadState()
        => Result<ControllerOutputState, VirtualControllerError>.Ok(ControllerOutputState.Empty);
    public void Dispose() => Disposed = true;
}

internal sealed class FakeHolder : Holder
{
    public int Closes { get; private set; }
    public int Updates { get; private set; }
    public int UpdatesAfterClose { get; private set; }
    public Action? DuringUpdate { get; set; }
    public FakeHolder()
    {
        Values = new ConcurrentDictionary<string, float>();
        Mappings = new Dictionary<string, string>();
        Flags = new Dictionary<string, bool>();
        SetMapping(Inputs.Wiimote.A, Inputs.Xbox360.A);
    }
    public override void Update()
    {
        DuringUpdate?.Invoke();
        Updates++;
        if (Closes > 0)
            UpdatesAfterClose++;
    }
    public override void Close() => Closes++;
    public override void AddMapping(ControllerType controller) { }
}

internal sealed class QueuedDispatcher : IUiDispatcher
{
    private readonly ConcurrentQueue<Action> _actions = new();
    public void Enqueue(Action action) => _actions.Enqueue(action);
    public void Drain()
    {
        while (_actions.TryDequeue(out var action))
            action();
    }
}

internal static class ControllerEvents
{
    // Inject hardware events into the real session without starting a WinUI application.
    public static void Publish(Nintroller controller, ControllerType type, INintrollerState state)
        => ((EventHandler<NintrollerStateEventArgs>)typeof(Nintroller)
            .GetField(nameof(Nintroller.StateUpdate), BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(controller)!)(controller, new(type, state, BatteryStatus.High));
    public static void ChangeExtension(Nintroller controller, ControllerType type)
        => ((EventHandler<NintrollerExtensionEventArgs>)typeof(Nintroller)
            .GetField(nameof(Nintroller.ExtensionChange), BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(controller)!)(controller, new(type));
}

internal sealed class FakeDiscovery(params DeviceInfo[] devices) : IDeviceDiscoveryService
{
    public Result<List<DeviceInfo>, DeviceDiscoveryError> DiscoverDevices()
        => Result<List<DeviceInfo>, DeviceDiscoveryError>.Ok(new(devices));
}

internal sealed class FakePreferences : IPreferencesService
{
    public bool AutoStartEnabled { get; set; }
    public bool StartMinimizedEnabled { get; set; }
    public bool ExclusiveBluetoothAccessEnabled { get; set; }
    public bool AutoRefreshEnabled { get; set; }
    public bool UseMicrosoftBluetoothStack { get; set; }
    public VirtualOutputMode VirtualOutputMode => VirtualOutputMode.ScpXbox360;
    public Result<Unit, PreferencesError> Save() => Result<Unit, PreferencesError>.Ok(Unit.Value);
    public void ApplyRuntimeSettings() { }
}

internal sealed class FakeOperations : IMainWindowOperations
{
    public Task SynchronizeAsync() => Task.CompletedTask;
    public Task RemoveAllWiimotesAsync() => Task.CompletedTask;
    public Task ShowDefaultCalibrationAsync() => Task.CompletedTask;
    public void DisconnectAllControllers() { }
    public void OpenControllerTestPanel() { }
    public void OpenUiGallery() { }
    public void SetAutoRefresh(bool enabled) { }
}
