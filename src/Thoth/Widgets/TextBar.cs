using Thoth.Rendering;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public class TextBar : IWidget, IWidgetWithLayout
{
    readonly TextBarScribe _scribe;

    public TextBar()
    {
        _scribe = new(this);
    }

    public string? LeftTitle { get; set; }

    public string? CenterTitle { get; set; }

    public string? RightTitle { get; set; }

    public Style? RightTitleStyle { get; set; }

    public string Line { get; set; } = "-";

    public Style Style { get; set; } = new();
    public IWidget Parent { get; set; } = SentinelWidget.Instance;

    public IWidgetRenderer GetRenderer() => _scribe;

    public IWidgetScribe GetScribe() => _scribe;

    public ILayoutCreator GetLayoutCreator() => new TextBarLayout();

    public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
    {
    }

    public void Accept<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IVisitor, allows ref struct
    {
    }
}
