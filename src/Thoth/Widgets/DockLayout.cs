using Thoth.Rendering;
using Thoth.Rendering.Layout;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public class DockLayout : ILayoutCreator
{
    public WidgetSizeRequest Measure(in IWidgetWithLayout widget,
                                     in SizeConstraint constraint,
                                     ReadOnlySpan<WidgetSizeRequest> requests)
    {
        var dock = widget as Dock
            ?? throw new InvalidOperationException($"{nameof(DockLayout)} requires {nameof(Dock)}.");

        if (dock.Content == null)
            return new(dock, this, new Size(0, 0));

        var size = requests[0].Size;
        if (dock.MaximumHeight.HasValue)
            size = new(size.Width, Math.Min(size.Height, dock.MaximumHeight.Value));

        return new(dock, this, size);
    }

    public void Arrange(in IWidgetWithLayout widget, in WidgetSize actual, ReadOnlySpan<WidgetSizeRequest> childRequests, Span<WidgetSize> children)
    {
        var dock = widget as Dock
            ?? throw new InvalidOperationException($"{nameof(DockLayout)} requires {nameof(Dock)}.");

        if (dock.Content == null)
            return;

        var child = dock.Content as IWidgetWithLayout
            ?? throw new InvalidOperationException($"{nameof(Dock)} content must implement {nameof(IWidgetWithLayout)}.");
        var height = dock.MaximumHeight.HasValue
            ? Math.Min(actual.Rect.Height, dock.MaximumHeight.Value)
            : actual.Rect.Height;

        children[0] = new WidgetSize(child,
                                     child.GetLayoutCreator(),
                                     new Rect(0, 0, actual.Rect.Width, height));
    }

    public void Draw(in IWidgetWithLayout widget, in Canvas canvas)
    {
        _ = widget;
        _ = canvas;
    }
}
