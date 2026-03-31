namespace Thoth.Widgets.Layout;

public class LayoutPlanner
{
    const int LayoutBufferSize = 256;

    readonly WidgetSizeRequest[] _measureBuffer = new WidgetSizeRequest[LayoutBufferSize];
    readonly WidgetSize[] _arrangeBuffer = new WidgetSize[LayoutBufferSize];

    HashSet<IWidgetWithLayout>? _layoutDirty = null;
    HashSet<IWidgetWithLayout>? _contentDirty = null;
    int _arrangeCount;

    public void Redraw(
        IWidgetWithLayout widget,
        in Thoth.Rendering.Layout.SizeConstraint constraint,
        Thoth.Rendering.Canvas canvas,
        Thoth.Rendering.FrameLayoutState layoutState)
    {
        var dirtyMap = GetDirtyMap();
        if (dirtyMap.IsLayoutDirty(widget) || _arrangeCount == 0)
        {
            var arrange = new ArrangeVisitor(in constraint, _measureBuffer, _arrangeBuffer);
            var measure = new MeasureVisitor(_measureBuffer, in constraint, arrange);
            measure.Visit(widget);
            _arrangeCount = measure.Count;

            layoutState.BeginLayout();
            for (var i = 0; i < _arrangeCount; i++)
            {
                var ws = _arrangeBuffer[i];
                layoutState.Set(ws.Child, ws.Rect);
            }
        }

        var draw = new DrawVisitor(canvas, _arrangeBuffer.AsSpan(0, _arrangeCount));
        if (_arrangeCount > 0)
            draw.Visit(widget);

        ClearDirty();
    }

    public void MarkLayoutDirty(IWidgetWithLayout widget)
    {
        _layoutDirty ??= new(ReferenceEqualityComparer.Instance);
        _layoutDirty.Add(widget);
    }

    public void MarkContentDirty(IWidgetWithLayout widget)
    {
        _contentDirty ??= new(ReferenceEqualityComparer.Instance);
        _contentDirty.Add(widget);
    }

    public DirtyMap GetDirtyMap() => new(_layoutDirty, _contentDirty);

    public void ClearDirty()
    {
        _layoutDirty = new(ReferenceEqualityComparer.Instance);
        _contentDirty = new(ReferenceEqualityComparer.Instance);
    }

    // public void Redraw(IWidgetWithLayout widget, in Thoth.Rendering.Layout.SizeConstraint constraint, ref DrawVisitor draw)
    // {
    //     Redraw(widget, constraint);
    //     draw.Draw(_arrange.Actual);
    // }
}
