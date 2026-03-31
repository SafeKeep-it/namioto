using Thoth.Rendering;
using Thoth.Rendering.Layout;

namespace Thoth.Widgets;

public sealed class OverlayWidgetScribe : IWidgetRenderer, IWidgetScribe
{
    readonly OverlayWidget _widget;
    readonly List<Canvas.ChildPlacement> _childPlacements = [];

    public OverlayWidgetScribe(OverlayWidget widget)
    {
        _widget = widget;
    }

    public Size Measure(SizeConstraint constraint)
    {
        return new(constraint.MaxWidth, constraint.MaxHeight);
    }

    public void Arrange(Rect rect)
    {
        _childPlacements.Clear();
        var bounds = new Rect(0, 0, rect.Width, rect.Height);
        _widget.Content.GetRenderer().Arrange(bounds);
        _childPlacements.Add(new(_widget.Content, bounds));
        if (_widget.ActiveOverlay is null) return;
        _widget._backdrop.GetRenderer().Arrange(bounds);
        _childPlacements.Add(new(_widget._backdrop, bounds));
        _widget.ActiveOverlay.GetRenderer().Arrange(bounds);
        _childPlacements.Add(new(_widget.ActiveOverlay, bounds));
    }

    public void Draw(Canvas canvas)
    {
        for (var i = 0; i < _childPlacements.Count; i++)
        {
            var placement = _childPlacements[i];
            canvas.RenderChild(_widget, in placement);
        }
    }
}
