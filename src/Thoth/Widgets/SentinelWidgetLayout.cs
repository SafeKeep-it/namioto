using Thoth.Rendering;
using Thoth.Rendering.Layout;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public class SentinelWidgetLayout : ILayoutCreator
{
    public WidgetSizeRequest Measure(in IWidgetWithLayout widget,
                                     in SizeConstraint constraint,
                                     ReadOnlySpan<WidgetSizeRequest> desires)
    {
        _ = constraint;
        _ = desires;
        return new(widget, this, new Size(0, 0));
    }

    public void Arrange(in IWidgetWithLayout widget, in WidgetSize actual, ReadOnlySpan<WidgetSizeRequest> childDesires, Span<WidgetSize> children)
    {
        _ = widget;
        _ = actual;
        _ = childDesires;
        _ = children;
    }

    public void Draw(in IWidgetWithLayout widget, in Canvas canvas)
    {
        _ = widget;
        _ = canvas;
    }
}
