using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Rendering;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public class Viewport : IWidget,
                         IViewport,
                         IWidgetWithLayout,
                         IEventHandler<Rendering.ScrollIntoViewCommand>,
                         IEventHandler<MouseScrollEvent>,
                         IEventHandler<KeyPressedInput>,
                        IEventHandler<OnLayoutChanged>
{
    readonly ViewportScribe _scribe;
    IWidget? _content;
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public ScrollDirection ScrollDirection { get; set; } = ScrollDirection.Vertical;
    public int ScrollStepY { get; set; } = 3;
    public int ScrollStepX { get; set; } = 4;
    public AutoScrollMode AutoScroll { get; set; } = AutoScrollMode.None;

    public IWidget? Content
    {
        get => _content;
        set
        {
            RenderPhaseGuard.ThrowIfActive("Viewport.Content set");
            if (_content != null)
                _content.Parent = SentinelWidget.Instance;

            _content = value;
            if (value != null)
                value.Parent = this;

            _scribe.ResetState();
        }
    }

    public Viewport()
    {
        _scribe = new(this);
    }

    public void Handle(IEventContext ctx, in MouseScrollEvent e)
    {
        if (!ctx.LayoutState.TryGetRect(this, out var r)) return;
        if (e.X < r.X || e.X >= r.X + r.Width || e.Y < r.Y || e.Y >= r.Y + r.Height) return;

        var changed = false;

        if (ScrollDirection.HasFlag(ScrollDirection.Horizontal))
        {
            var oldX = OffsetX;
            var maxScrollX = Math.Max(0, _scribe.LastChildSize.Width - _scribe.LastViewportSize.Width);
            OffsetX = Math.Clamp(OffsetX + e.Delta * ScrollStepX, 0, maxScrollX);
            if (OffsetX != oldX)
            {
                changed = true;
                ctx.RaiseEvent(new OnContentChanged());
            }
        }

        if (ScrollDirection.HasFlag(ScrollDirection.Vertical))
        {
            var oldY = OffsetY;
            var maxScrollY = Math.Max(0, _scribe.LastChildSize.Height - _scribe.LastViewportSize.Height);
            OffsetY = Math.Clamp(OffsetY + e.Delta * ScrollStepY, 0, maxScrollY);
            if (OffsetY != oldY)
            {
                changed = true;
                ctx.RaiseEvent(new OnContentChanged());
            }
        }

        if (changed) ctx.IsHandled = true;
    }

    public void Handle(IEventContext ctx, in KeyPressedInput e)
    {
        var changed = false;

        if (ScrollDirection.HasFlag(ScrollDirection.Vertical))
        {
            if (e.Key.Key == ConsoleKey.UpArrow)
            {
                var old = OffsetY;
                OffsetY = Math.Max(0, OffsetY - 1);
                changed = OffsetY != old;
            }
            else if (e.Key.Key == ConsoleKey.DownArrow)
            {
                var old = OffsetY;
                var maxScrollY = Math.Max(0, _scribe.LastChildSize.Height - _scribe.LastViewportSize.Height);
                OffsetY = Math.Min(maxScrollY, OffsetY + 1);
                changed = OffsetY != old;
            }
        }

        if (!changed) return;

        ctx.RaiseEvent(new OnContentChanged());
        ctx.IsHandled = true;
    }

    public void Handle(IEventContext ctx, in Rendering.ScrollIntoViewCommand e)
    {
        var relativeX = e.Region.X;
        var relativeY = e.Region.Y;
        var current = e.Sender;

        while (!ReferenceEquals(current, Content) && !ReferenceEquals(current, SentinelWidget.Instance))
        {
            if (!ctx.LayoutState.TryGetRect(current, out var rect)) return;
            relativeX += rect.X;
            relativeY += rect.Y;
            current = current.Parent;
        }

        if (current != Content) return;

        var viewportWidth = _scribe.LastViewportSize.Width;
        var viewportHeight = _scribe.LastViewportSize.Height;

        if (relativeY < OffsetY)
            OffsetY = relativeY;
        else if (relativeY + e.Region.Height > OffsetY + viewportHeight)
            OffsetY = relativeY + e.Region.Height - viewportHeight;

        if (relativeX < OffsetX)
            OffsetX = relativeX;
        else if (relativeX + e.Region.Width > OffsetX + viewportWidth)
            OffsetX = relativeX + e.Region.Width - viewportWidth;

        ctx.IsHandled = true;
    }

    public void Handle(IEventContext context, in OnLayoutChanged @event)
    {
        if (_scribe.LastViewportSize.Width <= 0 || _scribe.LastViewportSize.Height <= 0) return;
        context.RaiseEvent(new OnContentChanged());
    }

    public IWidget Parent { get; set; } = SentinelWidget.Instance;

    public IWidgetRenderer GetRenderer() => _scribe;

    public IWidgetScribe GetScribe() => _scribe;

    public ILayoutCreator GetLayoutCreator() => new ViewportLayout();

    public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
    {
        if (_content != null) visitor.Visit(_content);
    }

    public void Accept<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IVisitor, allows ref struct
    {
        if (_content == null)
            return;

        visitor.Visit((IWidgetWithLayout)_content);
    }
}
