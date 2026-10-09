using System;
using Microsoft.UI.Dispatching;

namespace WiinUSoft.Services;

public interface IUiDispatcher
{
    void Enqueue(Action action);
}

public sealed class DispatcherQueueUiDispatcher : IUiDispatcher
{
    private readonly DispatcherQueue _queue;

    public DispatcherQueueUiDispatcher(DispatcherQueue queue)
        => _queue = queue ?? throw new ArgumentNullException(nameof(queue));

    public void Enqueue(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (!_queue.HasThreadAccess)
            _queue.TryEnqueue(() => action());
        else
            action();
    }
}

public sealed class InlineUiDispatcher : IUiDispatcher
{
    public void Enqueue(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }
}
