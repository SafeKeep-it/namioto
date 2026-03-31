using Thoth.Rendering;
using Thoth.Rendering.Layout;

namespace Thoth.Widgets.Layout;

public struct ArrangeVisitor : ILayoutVisitor
{
    readonly SizeConstraint _constraint;
    readonly WidgetSizeRequest[] _measureRequests;
    readonly WidgetSize[] _outputBuffer;
    int _nodeIndex;

    public int Count { get; private set; }

    public ArrangeVisitor(
        in SizeConstraint constraint,
        WidgetSizeRequest[] measureRequests,
        WidgetSize[] outputBuffer)
    {
        _constraint = constraint;
        _measureRequests = measureRequests;
        _outputBuffer = outputBuffer;
        _nodeIndex = 0;
    }

    public readonly ArrangeResult Result => new ArrangeResult(_outputBuffer.AsSpan(0, Count));

    public void Visit(IWidgetWithLayout widget)
    {
        if (_nodeIndex == 0)
        {
            if (!ReferenceEquals(_measureRequests[0].Child, widget))
                throw new InvalidOperationException("Root widget mismatch between measure and arrange");
            _outputBuffer[0] = new WidgetSize(_measureRequests[0].Child, _measureRequests[0].Renderer,
                new Rect(0, 0, _constraint.MaxWidth, _constraint.MaxHeight));
            _nodeIndex++;
            ArrangeNode(widget, 0);
            Count = _nodeIndex;
        }
        else
        {
            if (!ReferenceEquals(_measureRequests[_nodeIndex].Child, widget))
                throw new InvalidOperationException($"Arrange topology mismatch at index {_nodeIndex}");
            
            _nodeIndex++;
        }
    }

    void ArrangeNode(IWidgetWithLayout widget, int nodeIndex)
    {
        int childrenStart = _nodeIndex;
        widget.Accept(ref this);
        int childrenEnd = _nodeIndex;

        _measureRequests[nodeIndex].Renderer.Arrange(
            widget,
            _outputBuffer[nodeIndex],
            _measureRequests.AsSpan(childrenStart, childrenEnd - childrenStart),
            _outputBuffer.AsSpan(childrenStart, childrenEnd - childrenStart));

        for (int i = childrenStart; i < childrenEnd; i++)
            ArrangeNode(_measureRequests[i].Child, i);
    }
}
