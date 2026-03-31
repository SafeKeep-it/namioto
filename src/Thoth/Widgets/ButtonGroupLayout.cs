using Thoth.Rendering;
using Thoth.Rendering.Layout;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public class ButtonGroupLayout : ILayoutCreator
{
    public WidgetSizeRequest Measure(in IWidgetWithLayout widget,
                                     in SizeConstraint constraint,
                                     ReadOnlySpan<WidgetSizeRequest> requests)
    {
        var group = widget as ButtonGroup
            ?? throw new InvalidOperationException($"{nameof(ButtonGroupLayout)} requires {nameof(ButtonGroup)}.");

        var gap = Math.Max(0, group.ButtonGap);
        var totalWidth = 0;
        var height = 0;
        for (var i = 0; i < requests.Length; i++)
        {
            totalWidth += requests[i].Size.Width;
            if (i > 0) totalWidth += gap;
            height = Math.Max(height, requests[i].Size.Height);
        }

        return new(group,
                   this,
                   new Size(Math.Min(totalWidth, constraint.MaxWidth), Math.Min(height, constraint.MaxHeight)));
    }

    public void Arrange(in IWidgetWithLayout widget, in WidgetSize actual, ReadOnlySpan<WidgetSizeRequest> childRequests, Span<WidgetSize> children)
    {
        var group = widget as ButtonGroup
            ?? throw new InvalidOperationException($"{nameof(ButtonGroupLayout)} requires {nameof(ButtonGroup)}.");

        var ordered = group.EnsureOrdered();
        var gap = Math.Max(0, group.ButtonGap);
        var x = 0;
        for (var i = 0; i < ordered.Count; i++)
        {
            var button = ordered[i];
            var size = childRequests[i].Size;
            var available = Math.Max(0, actual.Rect.Width - x);
            var width = Math.Min(size.Width, available);
            var height = Math.Min(size.Height, actual.Rect.Height);

            children[i] = new WidgetSize(button,
                                         button.GetLayoutCreator(),
                                         new Rect(x, 0, width, height));
            x += width + gap;
        }
    }

    public void Draw(in IWidgetWithLayout widget, in Canvas canvas)
    {
        _ = widget;
        _ = canvas;
    }
}
