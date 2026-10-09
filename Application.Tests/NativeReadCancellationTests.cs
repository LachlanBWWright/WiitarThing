using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using NintrollerLib;
using Shared.Windows;
using Xunit;

namespace Application.Tests;

public sealed class NativeReadCancellationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WindowsStreamCancelsPendingNativeReads(bool arrayOverload)
    {
        string name = "WiitarThing-tests-" + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var connection = server.WaitForConnectionAsync();
        using var stream = new WinBtStream(@"\\.\pipe\" + name);
        Assert.True(stream.TryOpenConnection().IsOk);
        await connection.WaitAsync(TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        var buffer = new byte[22];
        var read = arrayOverload
            ? stream.ReadAsync(buffer, 0, buffer.Length, cancellation.Token)
            : stream.ReadAsync(buffer.AsMemory(), cancellation.Token).AsTask();
        Assert.False(read.IsCompleted);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task StopCompletesWithoutAPacketAndRestartReadsFromTheSameConnection()
    {
        string name = "WiitarThing-tests-" + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var connection = server.WaitForConnectionAsync();
        using var stream = new WinBtStream(@"\\.\pipe\" + name);
        Assert.True(stream.TryOpenConnection().IsOk);
        await connection.WaitAsync(TimeSpan.FromSeconds(5));
        await using var controller = new Nintroller(stream);
        int disconnects = 0;
        controller.Disconnected += (_, _) => disconnects++;
        controller.BeginReading();
        controller.BeginReading();
        await controller.StopReadingAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(controller.Connected);
        Assert.True(stream.CanRead);
        Assert.Equal(0, disconnects);
        for (int iteration = 0; iteration < 20; iteration++)
        {
            controller.BeginReading();
            controller.StopReading();
        }
        await controller.ReadingCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        controller.BeginReading();
        server.Dispose(); // Native EOF now belongs to the restarted generation.
        await controller.ReadingCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, disconnects);
        Assert.False(controller.Connected);
    }

    [Fact]
    public async Task DisposingAnIdleControllerCompletesItsNativeReadLoop()
    {
        string name = "WiitarThing-tests-" + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var connection = server.WaitForConnectionAsync();
        using var stream = new WinBtStream(@"\\.\pipe\" + name);
        Assert.True(stream.TryOpenConnection().IsOk);
        await connection.WaitAsync(TimeSpan.FromSeconds(5));
        var controller = new Nintroller(stream);
        int disconnects = 0;
        controller.Disconnected += (_, _) => disconnects++;
        controller.BeginReading();
        await controller.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(controller.ReadingCompletion.IsCompletedSuccessfully);
        Assert.False(stream.CanRead);
        Assert.Equal(0, disconnects);
        Assert.Throws<ObjectDisposedException>(controller.BeginReading);
    }

    [Fact]
    public void BluetoothOptionsBelongToEachConnection()
    {
        using var exclusive = new WinBtStream("first", WinBtStream.BtStack.Toshiba, FileShare.None);
        using var shared = new WinBtStream("second", WinBtStream.BtStack.Microsoft, FileShare.ReadWrite);
        Assert.True(exclusive.UseToshiba);
        Assert.True(exclusive.UseFullReportSize);
        Assert.Equal(FileShare.None, exclusive.SharingMode);
        Assert.False(exclusive.AllowSharingRetry);
        Assert.False(shared.UseToshiba);
        Assert.False(shared.UseFullReportSize);
        Assert.Equal(FileShare.ReadWrite, shared.SharingMode);
    }
}
