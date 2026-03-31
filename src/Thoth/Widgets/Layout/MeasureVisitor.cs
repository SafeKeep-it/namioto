namespace Thoth.Widgets.Layout;

public struct MeasureVisitor : IVisitor
{
    readonly Thoth.Rendering.Layout.SizeConstraint _constraint;
    readonly ILayoutVisitor _layoutVisitor;
    WidgetSizeRequest[] _buffer;
    int _nodeIndex;

    public MeasureVisitor(
        WidgetSizeRequest[] buffer,
        in Thoth.Rendering.Layout.SizeConstraint constraint,
        ILayoutVisitor layoutVisitor)
    {
        _constraint = constraint;
        _layoutVisitor = layoutVisitor;
        _buffer = buffer;
        _nodeIndex = 0;
    }

    public int Count { get; private set; }

    public MeasureResult Result => new(_buffer.AsSpan(0, Count));

    public void Visit(IWidgetWithLayout widget)
    {
        if (_nodeIndex == 0)
        {
            _nodeIndex = 1;
            MeasureNode(widget, 0);
            Count = _nodeIndex;
            _layoutVisitor.Visit(widget);
        }
        else
        {
            _buffer[_nodeIndex] = new WidgetSizeRequest(widget, NullLayoutCreator.Instance, default);
            _nodeIndex++;
        }
    }

    void MeasureNode(IWidgetWithLayout widget, int nodeIndex)
    {
        int childrenStart = _nodeIndex;
        widget.Accept(ref this);
        int childrenEnd = _nodeIndex;

        for (int i = childrenStart; i < childrenEnd; i++)
            MeasureNode(_buffer[i].Child, i);

        var childRequests = _buffer[childrenStart..childrenEnd];
        _buffer[nodeIndex] = widget.GetLayoutCreator().Measure(widget, _constraint, childRequests);
    }
}
