using Thoth.Rendering;
using Thoth.Rendering.Layout;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public class DockPanelLayout : ILayoutCreator
{
    public WidgetSizeRequest Measure(in IWidgetWithLayout widget,
                                     in SizeConstraint constraint,
                                     ReadOnlySpan<WidgetSizeRequest> desires)
    {
        _ = widget as DockPanel
            ?? throw new InvalidOperationException($"{nameof(DockPanelLayout)} requires {nameof(DockPanel)}.");
        _ = desires;
        return new(widget, this, new Size(constraint.MaxWidth, constraint.MaxHeight));
    }

    public void Arrange(in IWidgetWithLayout widget, in WidgetSize actual, ReadOnlySpan<WidgetSizeRequest> childDesires, Span<WidgetSize> children)
    {
        var panel = widget as DockPanel
            ?? throw new InvalidOperationException($"{nameof(DockPanelLayout)} requires {nameof(DockPanel)}.");

        var remainingRect = new Rect(0, 0, actual.Rect.Width, actual.Rect.Height);
        var fillDocks = new List<(Dock Dock, int Index)>();

        for (var i = 0; i < panel.Children.Count; i++)
        {
            var child = (IWidgetWithLayout)panel.Children[i];
            if (child is not Dock dock) continue;

            var dockCreator = dock.GetLayoutCreator();

            if (dock.Position == DockPosition.Top)
            {
                var height = childDesires[i].Size.Height;
                height = Math.Min(height, Math.Max(0, remainingRect.Height));
                var childRect = new Rect(remainingRect.X, remainingRect.Y, Math.Max(0, remainingRect.Width), height);
                children[i] = new WidgetSize(dock, dockCreator, childRect);
                remainingRect = new Rect(remainingRect.X,
                                         remainingRect.Y + height,
                                         remainingRect.Width,
                                         Math.Max(0, remainingRect.Height - height));
            }
            else if (dock.Position == DockPosition.Bottom)
            {
                var height = childDesires[i].Size.Height;
                height = Math.Min(height, Math.Max(0, remainingRect.Height));
                var childRect = new Rect(remainingRect.X,
                                         remainingRect.Y + Math.Max(0, remainingRect.Height - height),
                                         Math.Max(0, remainingRect.Width),
                                         height);
                children[i] = new WidgetSize(dock, dockCreator, childRect);
                remainingRect = new Rect(remainingRect.X,
                                         remainingRect.Y,
                                         remainingRect.Width,
                                         Math.Max(0, remainingRect.Height - height));
            }
            else
            {
                fillDocks.Add((dock, i));
            }

            if (remainingRect.Width <= 0 || remainingRect.Height <= 0)
                break;
        }

        for (var i = 0; i < fillDocks.Count; i++)
        {
            var entry = fillDocks[i];
            children[entry.Index] = new WidgetSize(entry.Dock,
                                                   entry.Dock.GetLayoutCreator(),
                                                   new Rect(remainingRect.X,
                                                            remainingRect.Y,
                                                            Math.Max(0, remainingRect.Width),
                                                            Math.Max(0, remainingRect.Height)));
        }
    }

    public void Draw(in IWidgetWithLayout widget, in Canvas canvas)
    {
        _ = widget;
        _ = canvas;
    }
}
