using Thoth.Rendering;
using Thoth.Widgets;

namespace Thoth.Eventing;

public class EventContext : IEventContext, IEventObserverContext, ICommandContext
{
    EventDispatcher _dispatcher = null!;
    IWidget _target = null!;
    IWidget _currentWidget = null!;
    FrameLayoutState _layoutState = null!;
    readonly Dictionary<IWidget, InvalidationKind> _invalidations =
        new(ReferenceEqualityComparer.Instance);

    public bool IsHandled { get; set; }
    public FrameLayoutState LayoutState => _layoutState;
    public IReadOnlyDictionary<IWidget, InvalidationKind> Invalidations => _invalidations;

    public void RaiseEvent<T>(in T @event) where T : struct
    {
        _dispatcher.TrackInvalidation(_currentWidget, @event);
        _dispatcher.Enqueue(_currentWidget, @event);
    }

    public void MarkHandled() => IsHandled = true;

    public void CaptureMouse()
    {
        _dispatcher.CaptureMouse(_currentWidget);
    }

    public void ReleaseMouse()
    {
        _dispatcher.ReleaseMouse(_currentWidget);
    }

    public void Clear() => _invalidations.Clear();

    internal void RecordInvalidation(IWidget target, InvalidationKind invalidation)
    {
        if (invalidation == InvalidationKind.None) return;

        if (_invalidations.TryGetValue(target, out var existing))
            _invalidations[target] = existing | invalidation;
        else
            _invalidations[target] = invalidation;
    }

    internal void Reset(EventDispatcher dispatcher, IWidget target, FrameLayoutState layoutState)
    {
        _dispatcher = dispatcher;
        _target = target;
        _currentWidget = target;
        _layoutState = layoutState;
        IsHandled = false;
    }

    internal void SetCurrentWidget(IWidget widget)
    {
        _currentWidget = widget;
    }
}
