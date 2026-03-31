using Thoth.Rendering;
using Thoth.Rendering.Layout;
using Thoth.Widgets;

namespace Thoth.Widgets.Layout;

public interface ILayoutCreator
{
    WidgetSizeRequest Measure(in IWidgetWithLayout widget, in SizeConstraint constraint,
        ReadOnlySpan<WidgetSizeRequest> requests);

    void Arrange(in IWidgetWithLayout widget, in WidgetSize actual,
        ReadOnlySpan<WidgetSizeRequest> childRequests,
        Span<WidgetSize> children);

    void Draw(in IWidgetWithLayout widget, in Canvas canvas);
}

internal sealed class NullLayoutCreator : ILayoutCreator
{
    public static readonly NullLayoutCreator Instance = new();
    public WidgetSizeRequest Measure(in IWidgetWithLayout widget, in SizeConstraint constraint, ReadOnlySpan<WidgetSizeRequest> requests) => default;
    public void Arrange(in IWidgetWithLayout widget, in WidgetSize actual, ReadOnlySpan<WidgetSizeRequest> childRequests, Span<WidgetSize> children) { }
    public void Draw(in IWidgetWithLayout widget, in Canvas canvas) { }
}

public struct WidgetSizeRequest(IWidgetWithLayout child, ILayoutCreator renderer, Size size)
{
    public IWidgetWithLayout Child = child;
    public ILayoutCreator Renderer = renderer;
    public Size Size = size;
}

public struct WidgetSize(IWidgetWithLayout child, ILayoutCreator renderer, Rect rect)
{
    public IWidgetWithLayout Child = child;
    public ILayoutCreator Renderer = renderer;
    public Rect Rect = rect;
}
