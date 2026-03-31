namespace Thoth.Widgets.Layout;

public ref struct DrawVisitor : IVisitor
{
    Thoth.Rendering.Canvas _canvas;
    Span<WidgetSize> _outputBuffer;
    int _nodeIndex;

    public DrawVisitor(Thoth.Rendering.Canvas canvas, Span<WidgetSize> outputBuffer)
    {
        _canvas = canvas;
        _outputBuffer = outputBuffer;
    }

    public void Visit(IWidgetWithLayout widget)
    {
        if (_nodeIndex == 0)
        {
            _nodeIndex = 1;
            DrawNode(widget, 0);
        }
        else
        {
            _nodeIndex++;
        }
    }

    void DrawNode(IWidgetWithLayout widget, int nodeIndex)
    {
        int childrenStart = _nodeIndex;
        widget.Accept(ref this);
        int childrenEnd = _nodeIndex;

        var ws = _outputBuffer[nodeIndex];
        var childCanvas = _canvas.Slice(ws.Rect);
        ws.Renderer.Draw(ws.Child, childCanvas);

        for (int i = childrenStart; i < childrenEnd; i++)
            DrawNode(_outputBuffer[i].Child, i);
    }
}
