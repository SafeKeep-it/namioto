using Thoth.Rendering;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public class Dock : IWidget,
                    IWidgetWithLayout
{
    readonly DockScribe _scribe;
    IWidget? _content;

    public Dock()
    {
        _scribe = new(this);
    }

    public DockPosition Position { get; set; } = DockPosition.Fill;
    public int? MaximumHeight { get; set; }

    public IWidget? Content
    {
        get => _content;
        init
        {
            if (_content != null)
                _content.Parent = SentinelWidget.Instance;

            _content = value;
            if (value != null)
                value.Parent = this;
        }
    }

    public IWidget Parent { get; set; } = SentinelWidget.Instance;

    public IWidgetRenderer GetRenderer() => _scribe;

    public IWidgetScribe GetScribe() => _scribe;

    public ILayoutCreator GetLayoutCreator() => new DockLayout();

    public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
    {
        if (_content != null) visitor.Visit(_content);
    }

    public void Accept<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IVisitor, allows ref struct
    {
        if (_content == null)
            return;

        visitor.Visit((IWidgetWithLayout)_content);
    }
}
