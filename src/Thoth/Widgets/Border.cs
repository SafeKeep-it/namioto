using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Rendering;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public class Border : IWidget,
                      IWidgetWithLayout,
                      IEventHandler<OnMouseEnter>,
                      IEventHandler<OnMouseLeave>
{
    public sealed class label_set
    {
        public string? TopCenter { get; set; }
        public string? BottomLeft { get; set; }

        internal bool HasAny =>
            !string.IsNullOrWhiteSpace(TopCenter) ||
            !string.IsNullOrWhiteSpace(BottomLeft);
    }

    readonly BorderScribe _scribe;
    bool _isHovered;
    public WidthSizeMode WidthSizeMode { get; set; } = WidthSizeMode.Fill;
    public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;

    public BorderStyle BorderStyle { get; set; } = BorderStyle.Single;
    public Style Style { get; set; } = new();
    public Color? BorderColor { get; set; }
    public Color? BackgroundColor { get; set; }
    public Color? HoverBorderColor { get; set; }
    public Color? HoverBackgroundColor { get; set; }
    public label_set Labels { get; } = new();

    public required IWidget Content
    {
        get;
        init
        {
            field = value;
            field.Parent = this;
        }
    }

    public IWidget Parent { get; set; } = SentinelWidget.Instance;

    public IWidgetRenderer GetRenderer() => _scribe;

    public IWidgetScribe GetScribe() => _scribe;

    public ILayoutCreator GetLayoutCreator() => new BorderLayout();

    public Border()
    {
        _scribe = new(this);
    }

    public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
    {
        visitor.Visit(Content);
    }

    public void Accept<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IVisitor, allows ref struct
    {
        visitor.Visit((IWidgetWithLayout)Content);
    }

    internal bool IsHovered => _isHovered;

    public void Handle(IEventContext ctx, in OnMouseEnter e)
    {
        if (_isHovered) return;
        _isHovered = true;
        ctx.RaiseEvent(new OnContentChanged());
    }

    public void Handle(IEventContext ctx, in OnMouseLeave e)
    {
        if (!_isHovered) return;
        _isHovered = false;
        ctx.RaiseEvent(new OnContentChanged());
    }
}
