namespace Thoth.Widgets;

public class StackPanelLayout : Thoth.Widgets.Layout.ILayoutCreator
{
    public Thoth.Widgets.Layout.WidgetSizeRequest Measure(in IWidgetWithLayout widget,
                                                          in Thoth.Rendering.Layout.SizeConstraint constraint,
                                                           ReadOnlySpan<Thoth.Widgets.Layout.WidgetSizeRequest> requests)
    {
        _ = widget as StackPanel
            ?? throw new InvalidOperationException($"{nameof(StackPanelLayout)} requires {nameof(StackPanel)}.");

        var totalHeight = 0;
        var maxWidth = 0;
        for (var i = 0; i < requests.Length; i++)
        {
            totalHeight += requests[i].Size.Height;
            maxWidth = Math.Max(maxWidth, requests[i].Size.Width);
        }

        _ = Math.Min(maxWidth, constraint.MaxWidth);

        var size = new Thoth.Rendering.Layout.Size(constraint.MaxWidth, totalHeight);
        return new(widget, this, size);
    }

    public void Arrange(in IWidgetWithLayout widget,
                        in Thoth.Widgets.Layout.WidgetSize actual,
                        ReadOnlySpan<Thoth.Widgets.Layout.WidgetSizeRequest> childRequests,
                        Span<Thoth.Widgets.Layout.WidgetSize> children)
    {
        var stackPanel = widget as StackPanel
            ?? throw new InvalidOperationException($"{nameof(StackPanelLayout)} requires {nameof(StackPanel)}.");

        var childCount = stackPanel.Items.Count;
        var totalHeight = 0;
        var maxWidth = Math.Max(0, actual.Rect.Width);

        for (var i = 0; i < childCount; i++)
        {
            totalHeight += childRequests[i].Size.Height;
        }

        var y = Math.Max(0, actual.Rect.Height - totalHeight);
        for (var i = 0; i < childCount; i++)
        {
            var childRequested = childRequests[i];
            var childWidget = (IWidgetWithLayout)stackPanel.Items[i];
            var childCreator = childWidget.GetLayoutCreator();
            var childWidth = Math.Min(childRequested.Size.Width, maxWidth);
            children[i] = new Thoth.Widgets.Layout.WidgetSize(childWidget,
                                                              childCreator,
                                                              new Thoth.Rendering.Rect(0,
                                                                                       y,
                                                                                       childWidth,
                                                                                       childRequested.Size.Height));
            y += childRequested.Size.Height;
        }
    }

    public void Draw(in IWidgetWithLayout widget, in Thoth.Rendering.Canvas canvas)
    {
        _ = widget;
        _ = canvas;
    }
}
