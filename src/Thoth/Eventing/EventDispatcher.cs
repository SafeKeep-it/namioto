using Thoth.Diagnostics;
using Thoth.Rendering;
using Thoth.Widgets;

namespace Thoth.Eventing;

public class EventDispatcher
{
    readonly EventContext _context = new();
    readonly EventInvalidationTracker _invalidationTracker;
    readonly Queue<EventEnvelope> _queue = new();
    readonly List<IPublishedHandler> _subscribers = [];
    readonly Dictionary<IWidget, TargetCache> _targetCaches =
        new(ReferenceEqualityComparer.Instance);
    FrameLayoutState _layoutState = new();
    long _layoutVersion = -1;
    IWidget? _mouseCapture;

    internal int QueueCount => _queue.Count;
    public EventContext EventContext => _context;
    public IWidget? MouseCapture => _mouseCapture;

    public EventDispatcher()
    {
        _invalidationTracker = new(_context);
    }

    public void Enqueue<T>(IWidget source, in T @event) where T : struct
    {
        var captured = @event;
        var capturedSource = source;
        _queue.Enqueue(new(source, d => d.Dispatch(capturedSource, captured)));
    }

    public void DispatchAll()
    {
        while (_queue.TryDequeue(out var envelope))
        {
            envelope.Dispatch(this);
        }
    }

    public void ProcessQueue() => DispatchAll();

    public void RegisterCommandHandlers(IWidget root)
    {
        ResetCommandHandlerRegistries();

        var stack = new Stack<IWidget>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var widget = stack.Pop();
            RegisterCommandHandler(widget);

            var visitor = new PushToStackVisitor(stack);
            WidgetTraversalExtensions.VisitChildrenReverse(widget, ref visitor);
        }
    }

    public bool DispatchCommand(object command)
    {
        return command switch
        {
            OverlayWidget.ShowOverlayCommand showOverlay => DispatchRegisteredCommand(showOverlay),
            OverlayWidget.CloseOverlayCommand closeOverlay => DispatchRegisteredCommand(closeOverlay),
            _ => false
        };
    }

    public bool DispatchCommand<TCommand>(IWidget target, in TCommand command) where TCommand : struct
    {
        if (target is not IHandleCommand<TCommand> handler) return false;

        _context.Reset(this, target, _layoutState);
        _context.SetCurrentWidget(target);
        handler.Handle(_context, command);
        return true;
    }

    public bool DispatchPublished(object message)
    {
        if (message is null) return false;

        var handled = false;
        for (var i = 0; i < _subscribers.Count; i++)
            handled |= _subscribers[i].Dispatch(message);

        return handled;
    }

    public void Subscribe<T>(Action<T> handler) where T : struct
    {
        _subscribers.Add(new PublishedHandler<T>(handler));
    }

    public void SetLayoutState(FrameLayoutState layoutState)
    {
        _layoutState = layoutState;

        if (_layoutVersion == layoutState.LayoutVersion) return;
        _layoutVersion = layoutState.LayoutVersion;
        _targetCaches.Clear();
    }

    public void Dispatch<T>(IWidget target, in T @event) where T : struct
    {
        var timeDispatch = RuntimeTiming.IsEnabled;
        var dispatchStartedAt = timeDispatch ? RuntimeTiming.CaptureTimestamp() : 0;

        _context.Reset(this, target, _layoutState);

        foreach (var widget in GetCapturePath<T>(target))
        {
            _context.SetCurrentWidget(widget);
            if (!_context.IsHandled)
            {
                if (widget is ICapture<T> capturer) capturer.Capture(_context, @event);
            }
        }

        foreach (var widget in GetBubblePath<T>(target))
        {
            _context.SetCurrentWidget(widget);
            if (!_context.IsHandled)
            {
                if (widget is IEventHandler<T> handler) handler.Handle(_context, @event);
            }
            else
            {
                if (widget is IEventObserver<T> observer) observer.Observe(_context, @event);
            }
        }

        for (var i = 0; i < _subscribers.Count; i++)
            _subscribers[i].Dispatch(@event);

        if (timeDispatch)
            RuntimeTiming.RecordSince("event.dispatch", dispatchStartedAt);
    }

    internal void TrackInvalidation<T>(IWidget target, in T @event) where T : struct
    {
        _invalidationTracker.Track(target, @event);
    }

    internal List<IWidget> GetCapturePath<T>(IWidget target) where T : struct
    {
        return GetRouteTable<T>(target).CapturePath;
    }

    internal List<IWidget> GetBubblePath<T>(IWidget target) where T : struct
    {
        return GetRouteTable<T>(target).BubblePath;
    }

    RouteTable GetRouteTable<T>(IWidget target) where T : struct
    {
        var cache = GetCache(target);
        var id = EventTypeId<T>.Id;
        if (!cache.RouteTables.TryGetValue(id, out var routeTable))
        {
            routeTable = new(BuildCapturePath<T>(cache.Ancestors), BuildBubblePath<T>(cache.Ancestors));
            cache.RouteTables[id] = routeTable;
        }

        return routeTable;
    }

    TargetCache GetCache(IWidget target)
    {
        if (_targetCaches.TryGetValue(target, out var cache)) return cache;

        cache = new() { Ancestors = BuildAncestors(target) };
        _targetCaches.Add(target, cache);
        return cache;
    }

    public void InvalidateCaches() => _targetCaches.Clear();

    internal void CaptureMouse(IWidget widget)
    {
        _mouseCapture = widget;
    }

    internal void ReleaseMouse(IWidget widget)
    {
        if (ReferenceEquals(_mouseCapture, widget))
            _mouseCapture = null;
    }

    static List<IWidget> BuildAncestors(IWidget target)
    {
        var path = new List<IWidget>();
        var current = target;
        while (current is not SentinelWidget)
        {
            path.Add(current);
            current = current.Parent;
        }

        path.Reverse();
        return path;
    }

    static List<IWidget> BuildCapturePath<T>(List<IWidget> ancestors) where T : struct
    {
        var path = new List<IWidget>();
        foreach (var widget in ancestors)
            if (widget is ICapture<T>)
                path.Add(widget);

        return path;
    }

    static List<IWidget> BuildBubblePath<T>(List<IWidget> ancestors) where T : struct
    {
        var path = new List<IWidget>();
        for (var i = ancestors.Count - 1; i >= 0; i--)
        {
            var widget = ancestors[i];
            if (widget is IEventHandler<T> or IEventObserver<T>) path.Add(widget);
        }

        return path;
    }

    static void ResetCommandHandlerRegistries()
    {
        ResetCommandHandlerRegistry<OverlayWidget.ShowOverlayCommand>();
        ResetCommandHandlerRegistry<OverlayWidget.CloseOverlayCommand>();
    }

    static void RegisterCommandHandler(IWidget widget)
    {
        TryRegisterCommandHandler<OverlayWidget.ShowOverlayCommand>(widget);
        TryRegisterCommandHandler<OverlayWidget.CloseOverlayCommand>(widget);
    }

    static void ResetCommandHandlerRegistry<TCommand>() where TCommand : struct
    {
        CommandHandlerRegistry<TCommand>.Clear();
    }

    static void TryRegisterCommandHandler<TCommand>(IWidget widget) where TCommand : struct
    {
        if (widget is not IHandleCommand<TCommand> handler) return;

        CommandHandlerRegistry<TCommand>.Register(widget, handler);
    }

    bool DispatchRegisteredCommand<TCommand>(in TCommand command) where TCommand : struct
    {
        if (CommandHandlerRegistry<TCommand>.Count == 0) return false;

        CommandHandlerRegistry<TCommand>.Dispatch(this, command);
        return true;
    }

    class TargetCache
    {
        public readonly Dictionary<int, RouteTable> RouteTables = new(8);
        public List<IWidget> Ancestors = null!;
    }

    readonly record struct RouteTable(List<IWidget> CapturePath, List<IWidget> BubblePath);

    interface IPublishedHandler
    {
        bool Dispatch(object message);
    }

    readonly record struct PublishedHandler<T>(Action<T> Handler) : IPublishedHandler where T : struct
    {
        public bool Dispatch(object message)
        {
            if (message is not T typedMessage) return false;

            Handler(typedMessage);
            return true;
        }
    }

    public static class CommandHandlerRegistry<TCommand> where TCommand : struct
    {
        static readonly List<registered_handler<TCommand>> Handlers = [];

        public static void Clear() => Handlers.Clear();

        public static void Register(IWidget widget, IHandleCommand<TCommand> handler)
        {
            for (var i = 0; i < Handlers.Count; i++)
                if (ReferenceEquals(Handlers[i].Widget, widget))
                    return;

            Handlers.Add(new(widget, handler));
        }

        public static void Dispatch(EventDispatcher dispatcher, in TCommand command)
        {
            foreach (var handler in Handlers)
            {
                dispatcher._context.Reset(dispatcher, handler.Widget, dispatcher._layoutState);
                dispatcher._context.SetCurrentWidget(handler.Widget);
                handler.Handler.Handle(dispatcher._context, command);
            }
        }

        public static int Count => Handlers.Count;
    }

    readonly record struct registered_handler<TCommand>(IWidget Widget, IHandleCommand<TCommand> Handler)
        where TCommand : struct;
}
