using Thoth.Rendering;
using Thoth.Rendering.Layout;

namespace Thoth.Widgets;

public sealed class MultipleChoiceListScribe : IWidgetRenderer, IWidgetScribe
{
    readonly MultipleChoiceList _widget;
    Canvas.ChildPlacement _tablePlacement;
    bool _hasTable;

    public MultipleChoiceListScribe(MultipleChoiceList widget)
    {
        _widget = widget;
    }

    public Size Measure(SizeConstraint constraint)
    {
        _widget.ApplyActiveRowStyles();
        return _widget.Table.GetRenderer().Measure(constraint);
    }

    public void Arrange(Rect rect)
    {
        _widget.ApplyActiveRowStyles();
        var tableRect = new Rect(0, 0, rect.Width, rect.Height);
        _widget.Table.GetRenderer().Arrange(tableRect);
        _tablePlacement = new(_widget.Table, tableRect);
        _hasTable = true;
    }

    public void Draw(Canvas canvas)
    {
        if (!_hasTable) return;
        canvas.RenderChild(_widget, in _tablePlacement);
    }
}
