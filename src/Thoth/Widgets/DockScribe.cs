using Thoth.Rendering;
using Thoth.Rendering.Layout;

namespace Thoth.Widgets;

public sealed class DockScribe : IWidgetRenderer, IWidgetScribe
{
    readonly Dock _widget;
    Canvas.ChildPlacement _childPlacement;

    public DockScribe(Dock widget)
    {
        _widget = widget;
    }

    public Size Measure(SizeConstraint constraint)
    {
        if (_widget.Content == null)
            return new(0, 0);

        var size = _widget.Content.GetRenderer().Measure(constraint);
        if (_widget.MaximumHeight.HasValue)
            size = new(size.Width, Math.Min(size.Height, _widget.MaximumHeight.Value));

        return size;
    }

    public void Arrange(Rect rect)
    {
        if (_widget.Content == null) return;

        var height = rect.Height;
        if (_widget.MaximumHeight.HasValue) height = Math.Min(height, _widget.MaximumHeight.Value);
        var childRect = new Rect(0, 0, rect.Width, height);
        _widget.Content.GetRenderer().Arrange(childRect);
        _childPlacement = new(_widget.Content, childRect);
    }

    public void Draw(Canvas canvas)
    {
        if (_widget.Content == null) return;
        canvas.RenderChild(_widget, in _childPlacement);
    }
}
