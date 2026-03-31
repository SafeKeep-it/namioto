using Thoth.Rendering;
using Thoth.Rendering.Layout;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public sealed class SentinelWidget : IWidget, IWidgetWithLayout
{
    static readonly SentinelWidgetScribe ScribeInstance = new();
    public static readonly SentinelWidget Instance = new();

    SentinelWidget()
    {
        Parent = this;
    }

    public IWidget Parent { get; set; }

    public IWidgetRenderer GetRenderer() => ScribeInstance;

    public IWidgetScribe GetScribe() => ScribeInstance;

    public ILayoutCreator GetLayoutCreator() => new SentinelWidgetLayout();

    public Size Measure(SizeConstraint constraint) => ScribeInstance.Measure(constraint);

    public void Arrange(Rect rect) => ScribeInstance.Arrange(rect);

    public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
    {
        _ = visitor;
    }

    public void Accept<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IVisitor, allows ref struct
    {
    }
}
