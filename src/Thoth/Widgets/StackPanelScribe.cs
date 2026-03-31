using Thoth.Rendering;
using Thoth.Rendering.Layout;

namespace Thoth.Widgets;

public sealed class StackPanelScribe : IWidgetRenderer, IWidgetScribe
{
    readonly StackPanel _widget;
    readonly List<Canvas.ChildPlacement> _childPlacements = [];

    public StackPanelScribe(StackPanel widget)
    {
        _widget = widget;
    }

    public Size Measure(SizeConstraint constraint)
    {
        return new(constraint.MaxWidth, MeasureChildren(constraint.MaxWidth).TotalHeight);
    }

    public void Arrange(Rect rect)
    {
        _childPlacements.Clear();
        var measurement = MeasureChildren(rect.Width);
        var y = Math.Max(0, rect.Height - measurement.TotalHeight);
        for (var i = 0; i < measurement.ChildRects.Count; i++)
        {
            var child = _widget.Items[i];
            var childRect = measurement.ChildRects[i] with { Y = y };
            child.GetRenderer().Arrange(childRect);
            _childPlacements.Add(new(child, childRect));
            y += childRect.Height;
        }
    }

    public void Draw(Canvas canvas)
    {
        for (var i = 0; i < _childPlacements.Count; i++)
        {
            var placement = _childPlacements[i];
            canvas.RenderChild(_widget, in placement);
        }
    }

    Measurement MeasureChildren(int availableWidth)
    {
        var childRects = new List<Rect>(_widget.Items.Count);
        var totalHeight = 0;

        for (var i = 0; i < _widget.Items.Count; i++)
        {
            var child = _widget.Items[i];
            var childSize = child.GetRenderer().Measure(new(availableWidth, int.MaxValue));
            var childWidth = Math.Min(childSize.Width, availableWidth);
            childRects.Add(new(0, 0, childWidth, childSize.Height));
            totalHeight += childSize.Height;
        }

        return new(totalHeight, childRects);
    }

    readonly record struct Measurement(int TotalHeight, List<Rect> ChildRects);
}
