using Thoth.Rendering;
using Thoth.Rendering.Layout;

namespace Thoth.Widgets;

public sealed class ViewportScribe : IWidgetRenderer, IWidgetScribe
{
    readonly Viewport _widget;
    Canvas.ChildPlacement _contentPlacement;
    bool _hasContent;

    public Size LastChildSize { get; private set; }
    public Size LastViewportSize { get; private set; }

    public ViewportScribe(Viewport widget)
    {
        _widget = widget;
    }

    public void ResetState()
    {
        LastChildSize = default;
        LastViewportSize = default;
        _hasContent = false;
    }

    public Size Measure(SizeConstraint constraint)
    {
        if (_widget.Content == null) return new(0, 0);
        return new(constraint.MaxWidth, constraint.MaxHeight);
    }

    public void Arrange(Rect rect)
    {
        var previousViewportSize = LastViewportSize;
        var previousChildSize = LastChildSize;
        LastViewportSize = new(rect.Width, rect.Height);

        if (_widget.Content == null)
        {
            LastChildSize = new(0, 0);
            _hasContent = false;
            return;
        }

        var relaxed = new SizeConstraint(
            _widget.ScrollDirection.HasFlag(ScrollDirection.Horizontal) ? int.MaxValue : rect.Width,
            _widget.ScrollDirection.HasFlag(ScrollDirection.Vertical) ? int.MaxValue : rect.Height);

        var previousMaxScrollY = Math.Max(0, previousChildSize.Height - previousViewportSize.Height);
        var wasAtTop = _widget.OffsetY <= 0;
        var wasAtBottom = _widget.OffsetY >= previousMaxScrollY;

        var childSize = _widget.Content.GetRenderer().Measure(relaxed);
        LastChildSize = childSize;

        var maxScrollY = Math.Max(0, childSize.Height - rect.Height);
        _widget.OffsetY = _widget.AutoScroll switch
        {
            AutoScrollMode.Top when wasAtTop => 0,
            AutoScrollMode.Bottom when wasAtBottom => maxScrollY,
            _ => Math.Clamp(_widget.OffsetY, 0, maxScrollY)
        };

        var anchoredY = -_widget.OffsetY;
        var childRect = new Rect(-_widget.OffsetX, anchoredY,
                                 Math.Max(rect.Width, childSize.Width),
                                 Math.Max(rect.Height, childSize.Height));

        _widget.Content.GetRenderer().Arrange(childRect);
        _contentPlacement = new(_widget.Content, childRect);
        _hasContent = true;
    }

    public void Draw(Canvas canvas)
    {
        if (!_hasContent) return;
        canvas.RenderChild(_widget, in _contentPlacement);
    }
}
