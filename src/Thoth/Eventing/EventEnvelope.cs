using Thoth.Widgets;

namespace Thoth.Eventing;

public readonly struct EventEnvelope
{
    public IWidget Source { get; }
    readonly Action<EventDispatcher> _dispatch;

    internal EventEnvelope(IWidget source, Action<EventDispatcher> dispatch)
    {
        Source = source;
        _dispatch = dispatch;
    }

    internal void Dispatch(EventDispatcher dispatcher) => _dispatch(dispatcher);
}