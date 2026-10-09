using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NintrollerLib;
using Xunit;

namespace Shared.Tests;

public sealed class NintrollerTransportTests
{
    [Fact]
    public void BeginReadingDoesNotCreateOverlappingReadChains()
    {
        using var stream = new FakeTransport();
        using var controller = new Nintroller(stream, ControllerType.Wiimote);

        controller.BeginReading();
        controller.BeginReading();

        Assert.Equal(1, stream.BeginReadCalls);
    }

    [Fact]
    public async Task StopThenBeginWaitsForThePreviousReadBeforeStartingANewChain()
    {
        using var stream = new FakeTransport();
        using var controller = new Nintroller(stream, ControllerType.Wiimote);

        controller.BeginReading();
        controller.StopReading();
        controller.BeginReading();

        Assert.Equal(1, stream.BeginReadCalls);
        stream.CompleteRead(ValidButtonsOnlyReport());
        await stream.WaitForReadAsync(2);
        Assert.Equal(2, stream.BeginReadCalls);
    }

    [Fact]
    public async Task CompleteReportIsPublishedOnce()
    {
        using var stream = new FakeTransport();
        using var controller = new Nintroller(stream, ControllerType.Wiimote);

        controller.BeginReading();
        stream.CompleteRead(ValidButtonsOnlyReport());
        await stream.WaitForReadAsync(2);

        Assert.True(controller.Connected);
        Assert.Equal(2, stream.BeginReadCalls);
    }

    [Fact]
    public void StoppedControllerDoesNotPublishAnInFlightReport()
    {
        using var stream = new FakeTransport();
        using var controller = new Nintroller(stream, ControllerType.Wiimote);
        var stateUpdates = 0;
        controller.StateUpdate += (_, _) => stateUpdates++;

        controller.BeginReading();
        controller.StopReading();
        stream.CompleteRead(ValidButtonsOnlyReport());

        Assert.Equal(0, stateUpdates);
    }

    [Fact]
    public void WriteFailureRaisesOnlyOneDisconnectNotification()
    {
        using var stream = new FakeTransport { ThrowOnWrite = true };
        using var controller = new Nintroller(stream, ControllerType.Wiimote);
        var disconnects = 0;
        controller.Disconnected += (_, _) => disconnects++;

        controller.BeginReading();
        controller.GetStatus();
        controller.GetStatus();

        Assert.Equal(1, disconnects);
        Assert.False(controller.Connected);
    }

    [Fact]
    public void PartialReadIsNotParsedAsACompleteReport()
    {
        using var stream = new FakeTransport();
        using var controller = new Nintroller(stream, ControllerType.Wiimote);
        var stateUpdates = 0;
        controller.StateUpdate += (_, _) => stateUpdates++;

        controller.BeginReading();
        stream.CompleteRead(new byte[] { (byte)InputReport.BtnsOnly, 0x00, 0x00 });

        Assert.Equal(0, stateUpdates);
    }

    [Fact]
    public async Task EmptyReadRaisesDisconnect()
    {
        using var stream = new FakeTransport();
        using var controller = new Nintroller(stream, ControllerType.Wiimote);
        var disconnects = 0;
        controller.Disconnected += (_, _) => disconnects++;

        controller.BeginReading();
        stream.CompleteRead(Array.Empty<byte>());
        await controller.ReadingCompletion.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, disconnects);
        Assert.False(controller.Connected);
    }

    [Fact]
    public void DisposingControllerClosesTheTransport()
    {
        var stream = new FakeTransport();
        using (var controller = new Nintroller(stream, ControllerType.Wiimote))
            controller.BeginReading();

        Assert.True(stream.Closed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestartDiscardsStaleReportsAndDisconnects(bool emptyReport)
    {
        using var stream = new FakeTransport();
        using var controller = new Nintroller(stream, ControllerType.Wiimote);
        var updates = 0;
        var disconnects = 0;
        controller.StateUpdate += (_, _) => updates++;
        controller.Disconnected += (_, _) => disconnects++;
        controller.BeginReading();
        await InitializeWiimote(stream);
        var initialReads = stream.BeginReadCalls;
        controller.StopReading();
        controller.BeginReading();

        stream.CompleteRead(emptyReport ? Array.Empty<byte>() : ValidButtonsOnlyReport());
        await stream.WaitForReadAsync(initialReads + 1);

        Assert.Equal(0, updates);
        Assert.Equal(0, disconnects);
        Assert.True(controller.Connected);
        Assert.Equal(initialReads + 1, stream.BeginReadCalls);
        stream.CompleteRead(ValidButtonsOnlyReport());
        await stream.WaitForReadAsync(initialReads + 2);
        Assert.Equal(1, updates);
        Assert.Equal(initialReads + 2, stream.BeginReadCalls);
    }

    [Fact]
    public async Task RestartIgnoresAnIoFailureFromThePreviousRead()
    {
        using var stream = new FakeTransport();
        using var controller = new Nintroller(stream, ControllerType.Wiimote);
        var disconnects = 0;
        controller.Disconnected += (_, _) => disconnects++;
        controller.BeginReading();
        controller.StopReading();
        controller.BeginReading();
        stream.ThrowOnEndRead = true;
        stream.CompleteRead(ValidButtonsOnlyReport());
        await stream.WaitForReadAsync(2);
        Assert.Equal(0, disconnects);
        Assert.True(controller.Connected);
        Assert.Equal(2, stream.BeginReadCalls);
    }

    [Fact]
    public void DisposedControllerCannotRestartReading()
    {
        using var stream = new FakeTransport();
        var controller = new Nintroller(stream, ControllerType.Wiimote);
        controller.BeginReading();
        controller.Dispose();
        Assert.Throws<ObjectDisposedException>(controller.BeginReading);
        Assert.False(controller.Connected);
        Assert.Equal(1, stream.BeginReadCalls);
    }

    private static byte[] ValidButtonsOnlyReport()
    {
        return new byte[22] { (byte)InputReport.BtnsOnly, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
    }

    private static async Task InitializeWiimote(FakeTransport stream)
    {
        var status = new byte[22];
        status[0] = (byte)InputReport.Status;
        status[6] = 0xC0;
        stream.CompleteRead(status);
        await stream.WaitForReadAsync(2);
        var memory = new byte[22];
        memory[0] = (byte)InputReport.ReadMem;
        stream.CompleteRead(memory); // Extension initialization acknowledgement.
        await stream.WaitForReadAsync(3);
        memory[3] = 0x50; // Six bytes in the extension identifier reply.
        stream.CompleteRead(memory); // All-zero extension identifier is a Wiimote.
        await stream.WaitForReadAsync(4);
    }

    internal sealed class FakeTransport : Stream
    {
        private int _beginReadCalls;
        public int BeginReadCalls => Volatile.Read(ref _beginReadCalls);
        public bool ThrowOnWrite { get; init; }
        public bool ThrowOnEndRead { get; set; }
        public bool Closed { get; private set; }

        public async Task WaitForReadAsync(int count)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (BeginReadCalls < count)
                await Task.Delay(1, timeout.Token);
        }
        private byte[]? pendingBuffer;
        private CompletedAsyncResult? pendingResult;
        private AsyncCallback? pendingCallback;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get; set; }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            if (pendingResult is not null)
                throw new InvalidOperationException("Overlapping native reads.");
            pendingBuffer = buffer;
            pendingCallback = callback;
            pendingResult = new CompletedAsyncResult(state);
            Interlocked.Increment(ref _beginReadCalls);
            return pendingResult;
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
        public override int EndRead(IAsyncResult asyncResult)
        {
            if (ThrowOnEndRead)
                throw new IOException("Synthetic read failure.");
            return ((CompletedAsyncResult)asyncResult).BytesRead;
        }

        public void CompleteRead(byte[] data)
        {
            if (pendingBuffer is null || pendingResult is null)
                throw new InvalidOperationException("No pending read is available.");

            Array.Copy(data, pendingBuffer, data.Length);
            pendingResult.BytesRead = data.Length;
            var callback = pendingCallback;
            var result = pendingResult;
            pendingBuffer = null;
            pendingResult = null;
            pendingCallback = null;
            callback?.Invoke(result);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (ThrowOnWrite)
                throw new IOException("Synthetic transport failure.");
        }

        public override int Read(byte[] buffer, int offset, int count) => 0;
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            Closed = true;
            if (pendingResult is not null)
                CompleteRead(Array.Empty<byte>());
            base.Dispose(disposing);
        }

        private sealed class CompletedAsyncResult : IAsyncResult
        {
            public CompletedAsyncResult(object? state) => AsyncState = state;

            public int BytesRead { get; set; }

            public object? AsyncState { get; }
            public WaitHandle AsyncWaitHandle { get; } = new ManualResetEvent(true);
            public bool CompletedSynchronously => false;
            public bool IsCompleted => true;
        }
    }
}
