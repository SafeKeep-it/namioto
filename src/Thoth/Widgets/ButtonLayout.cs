using Thoth.Rendering;
using Thoth.Rendering.Layout;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public class ButtonLayout : ILayoutCreator
{
    public WidgetSizeRequest Measure(in IWidgetWithLayout widget,
                                     in SizeConstraint constraint,
                                     ReadOnlySpan<WidgetSizeRequest> requests)
    {
        var button = widget as Button
            ?? throw new InvalidOperationException($"{nameof(ButtonLayout)} requires {nameof(Button)}.");

        var requested = requests[0].Size;
        var minWidth = Math.Max(0, button.MinWidth);
        var width = Math.Max(requested.Width, minWidth);
        return new(button, this, new Size(Math.Min(width, constraint.MaxWidth), requested.Height));
    }

    public void Arrange(in IWidgetWithLayout widget, in WidgetSize actual, ReadOnlySpan<WidgetSizeRequest> childRequests, Span<WidgetSize> children)
    {
        var button = widget as Button
            ?? throw new InvalidOperationException($"{nameof(ButtonLayout)} requires {nameof(Button)}.");

        children[0] = new WidgetSize(button._chrome,
                                     button._chrome.GetLayoutCreator(),
                                     new Rect(0, 0, actual.Rect.Width, actual.Rect.Height));
    }

    public void Draw(in IWidgetWithLayout widget, in Canvas canvas)
    {
        _ = widget;
        _ = canvas;
    }
}
