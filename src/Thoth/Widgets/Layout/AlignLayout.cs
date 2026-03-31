using Thoth.Rendering;
using Thoth.Rendering.Layout;

namespace Thoth.Widgets.Layout;

public class AlignLayout : ILayoutCreator
{
    public WidgetSizeRequest Measure(in IWidgetWithLayout widget,
                                     in SizeConstraint constraint,
                                     ReadOnlySpan<WidgetSizeRequest> desires)
    {
        _ = desires;
        var align = widget as Align
            ?? throw new InvalidOperationException($"{nameof(AlignLayout)} requires {nameof(Align)}.");

        if (align.Content is null) return new(widget, this, new Size(0, 0));

        var child = (IWidgetWithLayout)align.Content;
        var childCreator = child.GetLayoutCreator();
        var childSize = childCreator.Measure(child, constraint, Span<WidgetSizeRequest>.Empty).Size;
        var width = align.WidthSizeMode == WidthSizeMode.Fill ? constraint.MaxWidth : childSize.Width;
        var height = align.HeightSizeMode == HeightSizeMode.Fill ? constraint.MaxHeight : childSize.Height;
        return new(widget, this, new Size(Math.Min(width, constraint.MaxWidth), Math.Min(height, constraint.MaxHeight)));
    }

    public void Arrange(in IWidgetWithLayout widget,
                        in WidgetSize actual,
                        ReadOnlySpan<WidgetSizeRequest> childDesires,
                        Span<WidgetSize> children)
    {
        var align = widget as Align
            ?? throw new InvalidOperationException($"{nameof(AlignLayout)} requires {nameof(Align)}.");

        if (align.Content is null || children.Length == 0) return;

        var child = (IWidgetWithLayout)align.Content;
        var childCreator = child.GetLayoutCreator();
        var maxWidth = Math.Max(0, actual.Rect.Width);
        var maxHeight = Math.Max(0, actual.Rect.Height);
        var childSize = childDesires[0].Size;
        var childWidth = Math.Min(childSize.Width, maxWidth);
        var childHeight = Math.Min(childSize.Height, maxHeight);
        var x = align.HorizontalAlignment switch
        {
            HorizontalAlignment.Center => (maxWidth - childWidth) / 2,
            HorizontalAlignment.Right => maxWidth - childWidth,
            var _ => 0
        };
        var y = align.VerticalAlignment switch
        {
            VerticalAlignment.Center => (maxHeight - childHeight) / 2,
            VerticalAlignment.Bottom => maxHeight - childHeight,
            var _ => 0
        };

        children[0] = new WidgetSize(child,
                                     childCreator,
                                     new Rect(x, y, childWidth, childHeight));
    }

    public void Draw(in IWidgetWithLayout widget, in Canvas canvas)
    {
        _ = widget;
        _ = canvas;
    }
}
