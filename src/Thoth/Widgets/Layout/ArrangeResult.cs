using Thoth.Rendering;
using Thoth.Widgets;

namespace Thoth.Widgets.Layout;

public readonly ref struct ArrangeResult
{
    readonly Span<WidgetSize> _widgetSizes;

    public ArrangeResult(Span<WidgetSize> widgetSizes) => _widgetSizes = widgetSizes;

    public Span<WidgetSize> WidgetSizes => _widgetSizes;
}
