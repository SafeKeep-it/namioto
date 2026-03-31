using Thoth.Rendering;
using Thoth.Rendering.Layout;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public class OverlayWidgetLayout : ILayoutCreator
{
    public WidgetSizeRequest Measure(in IWidgetWithLayout widget,
                                     in SizeConstraint constraint,
                                     ReadOnlySpan<WidgetSizeRequest> desires)
    {
        _ = desires;
        _ = widget as OverlayWidget
            ?? throw new InvalidOperationException($"{nameof(OverlayWidgetLayout)} requires {nameof(OverlayWidget)}.");
        return new(widget, this, new Size(constraint.MaxWidth, constraint.MaxHeight));
    }

    public void Arrange(in IWidgetWithLayout widget,
                        in WidgetSize actual,
                        ReadOnlySpan<WidgetSizeRequest> childDesires,
                        Span<WidgetSize> children)
    {
        _ = childDesires;
        var overlayWidget = widget as OverlayWidget
            ?? throw new InvalidOperationException($"{nameof(OverlayWidgetLayout)} requires {nameof(OverlayWidget)}.");

        var bounds = new Rect(0, 0, Math.Max(0, actual.Rect.Width), Math.Max(0, actual.Rect.Height));

        if (children.Length > 0)
        {
            var content = (IWidgetWithLayout)overlayWidget.Content;
            children[0] = new WidgetSize(content, content.GetLayoutCreator(), bounds);
        }

        if (children.Length > 1)
        {
            var backdropRect = overlayWidget.ActiveOverlay is null ? new Rect(0, 0, 0, 0) : bounds;
            children[1] = new WidgetSize(overlayWidget._backdrop, overlayWidget._backdrop.GetLayoutCreator(), backdropRect);
        }

        if (overlayWidget.ActiveOverlay is null || children.Length <= 2) return;

        var activeOverlay = (IWidgetWithLayout)overlayWidget.ActiveOverlay;
        children[2] = new WidgetSize(activeOverlay, activeOverlay.GetLayoutCreator(), bounds);
    }

    public void Draw(in IWidgetWithLayout widget, in Canvas canvas)
    {
        _ = widget;
        _ = canvas;
    }
}

internal sealed class OverlayBackdropLayout : ILayoutCreator
{
    public WidgetSizeRequest Measure(in IWidgetWithLayout widget,
                                     in SizeConstraint constraint,
                                     ReadOnlySpan<WidgetSizeRequest> desires)
    {
        _ = desires;
        _ = widget as OverlayWidget.OverlayBackdrop
            ?? throw new InvalidOperationException($"{nameof(OverlayBackdropLayout)} requires {nameof(OverlayWidget.OverlayBackdrop)}.");
        return new(widget, this, new Size(constraint.MaxWidth, constraint.MaxHeight));
    }

    public void Arrange(in IWidgetWithLayout widget,
                        in WidgetSize actual,
                        ReadOnlySpan<WidgetSizeRequest> childDesires,
                        Span<WidgetSize> children)
    {
        _ = widget as OverlayWidget.OverlayBackdrop
            ?? throw new InvalidOperationException($"{nameof(OverlayBackdropLayout)} requires {nameof(OverlayWidget.OverlayBackdrop)}.");
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
