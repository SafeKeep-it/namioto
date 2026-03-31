using Thoth.Widgets;

namespace Thoth.Widgets.Layout;

public readonly ref struct MeasureResult
{
    readonly Span<WidgetSizeRequest> _sizeRequests;

    public MeasureResult(Span<WidgetSizeRequest> requests) => _sizeRequests = requests;

    public Span<WidgetSizeRequest> SizeRequests => _sizeRequests;
}
