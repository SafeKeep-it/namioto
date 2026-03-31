using Thoth.Rendering;

namespace Thoth.Widgets;

public class Screen : IWidget, IWidgetWithLayout
{
    readonly ScreenScribe _scribe;
    readonly List<IWidget> _children = [];

    public Screen()
    {
        _scribe = new(this);
    }

    public string Title { get; set; } = string.Empty;
    public Style Style { get; set; } = new();
    public Color? ForegroundColor
    {
        get => Style.Foreground;
        set => Style = Style with { Foreground = value };
    }

    public Color? BackgroundColor
    {
        get => Style.Background;
        set => Style = Style with { Background = value };
    }

    public IWidget Parent { get; set; } = SentinelWidget.Instance;

    public IWidgetRenderer GetRenderer() => _scribe;

    public IWidgetScribe GetScribe() => _scribe;

    public Thoth.Widgets.Layout.ILayoutCreator GetLayoutCreator() => new ScreenLayout();

    public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
    {
        for (var i = 0; i < _children.Count; i++)
        {
            if (!visitor.Visit(_children[i])) return;
        }
    }

    public void Accept<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IVisitor, allows ref struct
    {
        for (var i = 0; i < _children.Count; i++)
            visitor.Visit((IWidgetWithLayout)_children[i]);
    }

    internal IReadOnlyList<IWidget> Children => _children;

    public void Add(IWidget child)
    {
        RenderPhaseGuard.ThrowIfActive("Screen.Add");
        child.Parent = this;
        _children.Add(child);
    }

    public void Remove(IWidget child)
    {
        RenderPhaseGuard.ThrowIfActive("Screen.Remove");
        child.Parent = SentinelWidget.Instance;
        _children.Remove(child);
    }
}
