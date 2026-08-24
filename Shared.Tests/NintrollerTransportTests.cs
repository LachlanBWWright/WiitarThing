using System;
using System.IO;
using System.Threading;
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

    private sealed class FakeTransport : Stream
    {
        public int BeginReadCalls { get; private set; }
        public bool ThrowOnWrite { get; init; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get; set; }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            BeginReadCalls++;
            return new CompletedAsyncResult(state);
        }

        public override int EndRead(IAsyncResult asyncResult) => 0;

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (ThrowOnWrite)
                throw new IOException("Synthetic transport failure.");
        }

        public override int Read(byte[] buffer, int offset, int count) => 0;
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        private sealed class CompletedAsyncResult : IAsyncResult
        {
            public CompletedAsyncResult(object? state) => AsyncState = state;

            public object? AsyncState { get; }
            public WaitHandle AsyncWaitHandle { get; } = new ManualResetEvent(true);
            public bool CompletedSynchronously => false;
            public bool IsCompleted => true;
        }
    }
}
