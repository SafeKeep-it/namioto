namespace Thoth.Widgets;

public class StackPanel : IWidget, IWidgetWithLayout
{
    readonly StackPanelScribe _scribe;

    public StackPanel()
    {
        _scribe = new(this);
        Items = new(this);
    }

    public IWidget Parent { get; set; } = SentinelWidget.Instance;

    public StackPanelItems Items { get; }

    public IWidgetRenderer GetRenderer() => _scribe;

    public IWidgetScribe GetScribe() => _scribe;

    public Thoth.Widgets.Layout.ILayoutCreator GetLayoutCreator() => new StackPanelLayout();

    public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
    {
        for (var i = 0; i < Items.Count; i++)
        {
            if (!visitor.Visit(Items[i])) return;
        }
    }

    public void Accept<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IVisitor, allows ref struct
    {
        for (var i = 0; i < Items.Count; i++)
            visitor.Visit((IWidgetWithLayout)Items[i]);
    }
}
