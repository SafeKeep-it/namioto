using System.Collections.Concurrent;

namespace Thoth.Terminal.Raw.Ingress;

public sealed class ConcurrentQueueStack<T>
{
    readonly ConcurrentQueue<T> _queue = new();
    readonly ConcurrentStack<T> _stack = new();

    public void Queue(T item)
    {
        _queue.Enqueue(item);
    }

    public void Push(T item)
    {
        _stack.Push(item);
    }

    public bool TryPop(out T? item)
    {
        if (_stack.TryPop(out item))
            return true;

        return _queue.TryDequeue(out item);
    }
}
