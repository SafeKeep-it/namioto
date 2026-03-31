using Thoth.Eventing.Events;
using Thoth.Rendering;
using Thoth.Rendering.Text;
using Thoth.Terminal;
using Thoth.Widgets;

namespace Thoth.Eventing;

public sealed class AttentionManager : IUiObserver
{
    readonly EventDispatcher _eventDispatcher;
    MouseButton? _mouseDownButton;
    IWidget _mouseDownTarget = SentinelWidget.Instance;
    readonly IWidget _root;
    readonly RootConsole _rootConsole;
    readonly UiContext _uiContext;
    bool _hasRenderedFrame;

    public AttentionManager(IWidget root, UiContext uiContext, EventDispatcher eventDispatcher, RootConsole rootConsole)
    {
        _root = root;
        _uiContext = uiContext;
        _eventDispatcher = eventDispatcher;
        _rootConsole = rootConsole;
    }

    public AttentionManager(ITerminal terminal,
                            IWidget root,
                            IWidget? keyboardFocus = null,
                            IWidthProvider? widthProvider = null)
    {
        _root = root;
        _uiContext = new(root, widthProvider);
        _eventDispatcher = new();
        _rootConsole = new(new(terminal), _uiContext);

        SetKeyboardFocus(keyboardFocus);
        _eventDispatcher.RegisterCommandHandlers(root);
        _eventDispatcher.Subscribe<CopyToClipboardRequested>(e => terminal.SetClipboard(e.Text));
    }

    public IWidget? KeyboardFocus => _uiContext.KeyboardFocus;

    public IWidget KeyboardTargetOr(IWidget fallback)
    {
        EnsureKeyboardFocus(_root);
        return KeyboardFocus ?? fallback;
    }

    public void On<T>(Action<T> handler) where T : struct => _eventDispatcher.Subscribe(handler);

    public void SendCommand(object command)
    {
        EnsureNotRenderingPhase("AttentionManager.SendCommand");
        var handled = _eventDispatcher.DispatchCommand(command);
        handled |= _eventDispatcher.DispatchPublished(command);
        if (handled)
            _eventDispatcher.EventContext.RecordInvalidation(_root, InvalidationKind.Layout);
    }

    public void Render()
    {
        _eventDispatcher.SetLayoutState(_rootConsole.LayoutState);
        _eventDispatcher.ProcessQueue();
        if (BindingUpdateQueue.Flush(_eventDispatcher))
            _eventDispatcher.ProcessQueue();

        _uiContext.IsRendering = true;
        _rootConsole.Render(_eventDispatcher.EventContext.Invalidations);
        _uiContext.IsRendering = false;
        BindingUpdateQueue.PruneDetached(_root);
        _hasRenderedFrame = true;

        _eventDispatcher.EventContext.Clear();
    }

    public bool TickAnimations(long nowTicks)
    {
        var stack = new Stack<IWidget>();
        stack.Push(_root);

        var anyChanged = false;

        while (stack.Count > 0)
        {
            var widget = stack.Pop();

            anyChanged |= widget switch
            {
                Spinner s when s.IsAnimationActive => s.UpdateAnimation(nowTicks),
                TextBlock t when t.IsAnimationActive => t.UpdateAnimation(nowTicks),
                _ => false
            };

            var visitor = new PushToStackVisitor(stack);
            WidgetTraversalExtensions.VisitChildrenReverse(widget, ref visitor);
        }

        return anyChanged;
    }

    public void HandleKey(ConsoleKeyInfo key)
    {
        EnsureNotRenderingPhase("AttentionManager.HandleKey");
        EnsureLayoutUpToDate();
        if (IsFocusTraversalKey(key) && TryMoveKeyboardFocus(key)) return;

        var target = ResolveKeyboardTarget();
        _eventDispatcher.Dispatch(target, new KeyPressedInput(key));
    }

    public void HandleText(string text)
    {
        EnsureNotRenderingPhase("AttentionManager.HandleText");
        EnsureFrameReady();
        var target = ResolveKeyboardTarget();
        _eventDispatcher.Dispatch(target, new TextInput(text));
    }

    public void HandlePaste(string text)
    {
        EnsureNotRenderingPhase("AttentionManager.HandlePaste");
        EnsureFrameReady();
        var target = ResolveKeyboardTarget();
        _eventDispatcher.Dispatch(target, new PasteInput(text));
    }

    public void HandleMouseDown(int x, int y, MouseButton button)
    {
        EnsureNotRenderingPhase("AttentionManager.HandleMouseDown");
        EnsureLayoutUpToDate();
        var hitTarget = _rootConsole.LayoutState.WidgetAt(x, y);
        var target = ResolveMouseTarget(hitTarget, _eventDispatcher.MouseCapture);
        SetMouseHover(target);
        BeginMouseDown(target, button);
        SetKeyboardFocusIfFocusable(target);

        if (!ReferenceEquals(target, SentinelWidget.Instance))
            _eventDispatcher.Dispatch(target, new OnMouseDown(x, y, button));
    }

    public void HandleMouseUp(int x, int y, MouseButton button)
    {
        EnsureNotRenderingPhase("AttentionManager.HandleMouseUp");
        EnsureLayoutUpToDate();
        var hitTarget = _rootConsole.LayoutState.WidgetAt(x, y);
        var target = ResolveMouseTarget(hitTarget, _eventDispatcher.MouseCapture);
        SetMouseHover(target);

        if (!ReferenceEquals(target, SentinelWidget.Instance))
            _eventDispatcher.Dispatch(target, new OnMouseUp(x, y, button));

        if (ShouldDispatchClick(target, button))
            _eventDispatcher.Dispatch(target, new OnMouseClick(x, y, button));

        EndMouseInteraction();
    }

    public void HandleMouseMove(int x, int y)
    {
        EnsureNotRenderingPhase("AttentionManager.HandleMouseMove");
        EnsureLayoutUpToDate();
        var hitTarget = _rootConsole.LayoutState.WidgetAt(x, y);
        var target = ResolveMouseTarget(hitTarget, _eventDispatcher.MouseCapture);
        SetMouseHover(target);

        if (!ReferenceEquals(target, SentinelWidget.Instance))
            _eventDispatcher.Dispatch(target, new OnMouseMove(x, y));
    }

    public void HandleScroll(int x, int y, int delta)
    {
        EnsureNotRenderingPhase("AttentionManager.HandleScroll");
        EnsureLayoutUpToDate();
        var hitTarget = _rootConsole.LayoutState.WidgetAt(x, y);
        var target = ResolveMouseTarget(hitTarget, _eventDispatcher.MouseCapture);
        if (ReferenceEquals(target, SentinelWidget.Instance)) target = _root;
        _eventDispatcher.Dispatch(target, new MouseScrollEvent(x, y, delta));
    }

    static void EnsureNotRenderingPhase(string operation)
    {
        RenderPhaseGuard.ThrowIfActive(operation);
    }

    void EnsureFrameReady()
    {
        if (_hasRenderedFrame) return;
        Render();
    }

    void EnsureLayoutUpToDate()
    {
        if (!_hasRenderedFrame || _eventDispatcher.EventContext.Invalidations.Count > 0)
            Render();
    }

    static bool IsFocusTraversalKey(ConsoleKeyInfo key)
    {
        return key.Key == ConsoleKey.Tab &&
               (key.Modifiers & ConsoleModifiers.Control) != 0;
    }

    bool TryMoveKeyboardFocus(ConsoleKeyInfo key)
    {
        var focusScope = FindActiveOverlay() ?? _root;
        EnsureKeyboardFocus(focusScope);
        if (KeyboardFocus is not { } currentFocus) return false;
        if (!_rootConsole.LayoutState.TryGetRect(currentFocus, out var currentRect)) return false;

        var candidates = CollectFocusableCandidates(currentFocus, focusScope);
        if (candidates.Count == 0) return false;

        var reverse = (key.Modifiers & ConsoleModifiers.Shift) != 0;
        candidates.Sort((left, right) => CompareCandidates(currentRect, left, right, reverse));

        for (var i = 0; i < candidates.Count; i++)
            if (TryAcceptKeyboardFocus(currentFocus, candidates[i].Widget))
                return true;

        return false;
    }

    List<FocusableCandidate> CollectFocusableCandidates(IWidget currentFocus, IWidget focusScope)
    {
        var candidates = new List<FocusableCandidate>();
        var items = _rootConsole.LayoutState.FocusableItems();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (ReferenceEquals(item.Widget, currentFocus)) continue;
            if (!IsDescendantOrSelf(item.Widget, focusScope)) continue;
            candidates.Add(new(item.Widget, item.Rect, item.DrawOrder));
        }

        return candidates;
    }

    static int CompareCandidates(Rect currentRect,
                                 FocusableCandidate left,
                                 FocusableCandidate right,
                                 bool reverse)
    {
        var leftBucket = DirectionBucket(currentRect, left.Rect, reverse);
        var rightBucket = DirectionBucket(currentRect, right.Rect, reverse);
        var bucketComparison = leftBucket.CompareTo(rightBucket);
        if (bucketComparison != 0) return bucketComparison;

        var leftDistance = RectDistanceSquared(currentRect, left.Rect);
        var rightDistance = RectDistanceSquared(currentRect, right.Rect);

        var distanceComparison = leftDistance.CompareTo(rightDistance);
        if (distanceComparison != 0) return distanceComparison;

        return right.DrawOrder.CompareTo(left.DrawOrder);
    }

    static int DirectionBucket(Rect current, Rect candidate, bool reverse)
    {
        var currentCx = current.X + (current.Width / 2);
        var currentCy = current.Y + (current.Height / 2);
        var candidateCx = candidate.X + (candidate.Width / 2);
        var candidateCy = candidate.Y + (candidate.Height / 2);

        var dx = candidateCx - currentCx;
        var dy = candidateCy - currentCy;
        var absDx = Math.Abs(dx);
        var absDy = Math.Abs(dy);

        if (reverse)
        {
            if (dx < 0 && absDx >= absDy) return 0;
            if (dy < 0 && absDy > absDx) return 1;
            if (dy > 0 && absDy >= absDx) return 2;
            if (dx > 0 && absDx > absDy) return 3;
            return 4;
        }

        if (dx > 0 && absDx >= absDy) return 0;
        if (dy > 0 && absDy > absDx) return 1;
        if (dy < 0 && absDy >= absDx) return 2;
        if (dx < 0 && absDx > absDy) return 3;
        return 4;
    }

    static long RectDistanceSquared(Rect a, Rect b)
    {
        var dx = AxisDistance(a.X, a.X + a.Width, b.X, b.X + b.Width);
        var dy = AxisDistance(a.Y, a.Y + a.Height, b.Y, b.Y + b.Height);
        return (long)dx * dx + (long)dy * dy;
    }

    static int AxisDistance(int aStart, int aEnd, int bStart, int bEnd)
    {
        if (aEnd <= bStart) return bStart - aEnd;
        if (bEnd <= aStart) return aStart - bEnd;
        return 0;
    }

    readonly record struct FocusableCandidate(IWidget Widget, Rect Rect, int DrawOrder);

    bool TryAcceptKeyboardFocus(IWidget previous, IWidget next)
    {
        _eventDispatcher.Dispatch(next, new OnFocus());
        if (!_eventDispatcher.EventContext.IsHandled) return false;

        _uiContext.KeyboardFocus = next;
        _eventDispatcher.Dispatch(previous, new OnBlur());
        return true;
    }

    public void SetKeyboardFocus(IWidget? next)
    {
        var previous = _uiContext.KeyboardFocus;
        if (ReferenceEquals(previous, next)) return;

        if (next == null)
        {
            _uiContext.KeyboardFocus = null;
            if (previous != null) _eventDispatcher.Dispatch(previous, new OnBlur());
            return;
        }

        _uiContext.KeyboardFocus = next;
        if (previous != null) _eventDispatcher.Dispatch(previous, new OnBlur());
        _eventDispatcher.Dispatch(next, new OnFocus());
    }

    public void SetMouseHover(IWidget? next)
    {
        var previous = _uiContext.MouseHover;
        if (ReferenceEquals(previous, next)) return;

        _uiContext.MouseHover = next;
        if (previous != null) _eventDispatcher.Dispatch(previous, new OnMouseLeave());
        if (next != null) _eventDispatcher.Dispatch(next, new OnMouseEnter());
    }

    public void SetKeyboardFocusIfFocusable(IWidget? target)
    {
        if (target is Navigation.Focus.IFocusable)
            SetKeyboardFocus(target);
    }

    IWidget ResolveKeyboardTarget()
    {
        var overlay = FindActiveOverlay();
        var focusScope = overlay ?? _root;
        EnsureKeyboardFocus(focusScope);
        if (KeyboardFocus is { } focus) return focus;
        return overlay ?? _root;
    }

    void EnsureKeyboardFocus(IWidget focusScope)
    {
        if (KeyboardFocus is { } focus && IsDescendantOrSelf(focus, focusScope)) return;

        var target = FindAutoFocusable(focusScope) ?? FindFocusable(focusScope);
        if (target != null)
            SetKeyboardFocus(target);
        else
            SetKeyboardFocus(null);
    }

    IWidget? FindActiveOverlay()
    {
        var stack = new Stack<IWidget>();
        stack.Push(_root);

        IWidget? selected = null;
        var selectedOrder = int.MinValue;

        while (stack.Count > 0)
        {
            var widget = stack.Pop();
            if (widget is IOverlayHost { ActiveOverlay: { } overlay })
            {
                var order = _rootConsole.LayoutState.DrawOrderOf(overlay);
                if (order >= selectedOrder)
                {
                    selectedOrder = order;
                    selected = overlay;
                }
            }

            var visitor = new PushToStackVisitor(stack);
            WidgetTraversalExtensions.VisitChildrenReverse(widget, ref visitor);
        }

        return selected;
    }

    static IWidget? FindAutoFocusable(IWidget root)
    {
        var stack = new Stack<IWidget>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var widget = stack.Pop();
            if (widget is Navigation.Focus.IAutoFocusable) return widget;
            var visitor = new PushToStackVisitor(stack);
            WidgetTraversalExtensions.VisitChildrenReverse(widget, ref visitor);
        }

        return null;
    }

    static IWidget? FindFocusable(IWidget root)
    {
        var stack = new Stack<IWidget>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var widget = stack.Pop();
            if (widget is Navigation.Focus.IFocusable) return widget;
            var visitor = new PushToStackVisitor(stack);
            WidgetTraversalExtensions.VisitChildrenReverse(widget, ref visitor);
        }

        return null;
    }

    public void BeginMouseDown(IWidget? target, MouseButton button)
    {
        _mouseDownTarget = target ?? SentinelWidget.Instance;
        _mouseDownButton = button;
    }

    public IWidget ResolveMouseTarget(IWidget hitTarget, IWidget? mouseCapture)
    {
        if (mouseCapture is not null) return mouseCapture;
        return hitTarget;
    }

    public bool ShouldDispatchClick(IWidget hitTarget, MouseButton button)
    {
        return !ReferenceEquals(hitTarget, SentinelWidget.Instance) &&
                ReferenceEquals(hitTarget, _mouseDownTarget) &&
                _mouseDownButton == button;
    }

    static bool IsDescendantOrSelf(IWidget widget, IWidget ancestor)
    {
        var current = widget;
        while (current is not SentinelWidget)
        {
            if (ReferenceEquals(current, ancestor)) return true;
            current = current.Parent;
        }

        return false;
    }

    public void EndMouseInteraction()
    {
        _mouseDownTarget = SentinelWidget.Instance;
        _mouseDownButton = null;
    }
}
