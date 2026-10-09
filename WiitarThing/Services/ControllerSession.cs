using System;
using NintrollerLib;

namespace WiinUSoft.Services;

/// <summary>Owns the lifetime of a controller transport independently of its view.</summary>
public sealed class ControllerSession : IDisposable
{
    private bool _disposed;
    private int _connectionLostRaised;

    public ControllerSession(Nintroller controller)
        => Controller = controller ?? throw new ArgumentNullException(nameof(controller));

    public Nintroller Controller { get; }

    internal DeviceSessionState State { get; } = new();

    internal bool TryAcceptConnectionLost()
        => System.Threading.Interlocked.Exchange(ref _connectionLostRaised, 1) == 0;

    internal void BeginConnection()
        => System.Threading.Interlocked.Exchange(ref _connectionLostRaised, 0);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Controller.Dispose();
    }
}
