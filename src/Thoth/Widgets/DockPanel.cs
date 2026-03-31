using Thoth.Rendering;

namespace Thoth.Widgets;

public class DockPanel : IWidget, IWidgetWithLayout
{
    readonly DockPanelScribe _scribe;
    internal readonly List<IWidget> Children = [];

    public DockPanel()
    {
        _scribe = new(this);
    }

    public IWidget Parent { get; set; } = SentinelWidget.Instance;

    public IWidgetRenderer GetRenderer() => _scribe;

    public IWidgetScribe GetScribe() => _scribe;

    public Thoth.Widgets.Layout.ILayoutCreator GetLayoutCreator() => new DockPanelLayout();

    public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
    {
        for (var i = 0; i < Children.Count; i++)
        {
            if (!visitor.Visit(Children[i])) return;
        }
    }

    public void Accept<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IVisitor, allows ref struct
    {
        for (var i = 0; i < Children.Count; i++)
            visitor.Visit((IWidgetWithLayout)Children[i]);
    }

    public void Add(IWidget child)
    {
        RenderPhaseGuard.ThrowIfActive("DockPanel.Add");
        child.Parent = this;
        Children.Add(child);
    }

    public void Remove(IWidget child)
    {
        RenderPhaseGuard.ThrowIfActive("DockPanel.Remove");
        child.Parent = SentinelWidget.Instance;
        Children.Remove(child);
    }
}
