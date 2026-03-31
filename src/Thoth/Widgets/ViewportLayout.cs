using Thoth.Rendering;
using Thoth.Rendering.Layout;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public class ViewportLayout : ILayoutCreator
{
    public WidgetSizeRequest Measure(in IWidgetWithLayout widget,
                                     in SizeConstraint constraint,
                                     ReadOnlySpan<WidgetSizeRequest> requests)
    {
        var viewport = widget as Viewport
            ?? throw new InvalidOperationException($"{nameof(ViewportLayout)} requires {nameof(Viewport)}.");

        if (viewport.Content == null)
            return new(viewport, this, new Size(0, 0));

        _ = requests[0].Size;
        return new(viewport, this, new Size(constraint.MaxWidth, constraint.MaxHeight));
    }

    public void Arrange(in IWidgetWithLayout widget, in WidgetSize actual, ReadOnlySpan<WidgetSizeRequest> childRequests, Span<WidgetSize> children)
    {
        var viewport = widget as Viewport
            ?? throw new InvalidOperationException($"{nameof(ViewportLayout)} requires {nameof(Viewport)}.");

        if (viewport.Content == null)
            return;

        var child = viewport.Content as IWidgetWithLayout
            ?? throw new InvalidOperationException($"{nameof(Viewport)} content must implement {nameof(IWidgetWithLayout)}.");
        var childLayout = child.GetLayoutCreator();
        var relaxed = new SizeConstraint(
            viewport.ScrollDirection.HasFlag(ScrollDirection.Horizontal) ? int.MaxValue : actual.Rect.Width,
            viewport.ScrollDirection.HasFlag(ScrollDirection.Vertical) ? int.MaxValue : actual.Rect.Height);
        var requested = childLayout.Measure(child, relaxed, Span<WidgetSizeRequest>.Empty);
        var childRect = new Rect(-viewport.OffsetX,
                                 -viewport.OffsetY,
                                 Math.Max(actual.Rect.Width, requested.Size.Width),
                                 Math.Max(actual.Rect.Height, requested.Size.Height));

        children[0] = new WidgetSize(child, childLayout, childRect);
    }

    public void Draw(in IWidgetWithLayout widget, in Canvas canvas)
    {
        _ = widget;
        _ = canvas;
    }
}
