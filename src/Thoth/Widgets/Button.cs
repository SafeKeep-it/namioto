using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Navigation.Focus;
using Thoth.Rendering;
using Thoth.Rendering.Layout;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public sealed class Button : IWidget,
                              IWidgetWithLayout,
                              IFocusable,
                              IEventHandler<OnFocus>,
                              IEventHandler<OnBlur>,
                             IEventHandler<OnMouseEnter>,
                             IEventHandler<OnMouseLeave>,
                             IEventHandler<OnMouseClick>
{
    readonly ButtonScribe _scribe;
    internal readonly Border _chrome;
    internal readonly TextBlock _label;
    Style _style = new();
    BorderStyle _borderStyle = BorderStyle.Inset;
    Color? _foregroundColor;
    Color? _backgroundColor;
    Color? _borderColor;
    string _text = string.Empty;

    public Button()
    {
        _label = new TextBlock
                 {
                     Overflow = TextOverflow.Clip
                 };
        _chrome = new Border
                  {
                      BorderStyle = BorderStyle.Inset,
                      Content = _label
                  };
        _scribe = new(this);
        _chrome.Parent = this;
        SyncChrome();
    }

    public IWidget Parent { get; set; } = SentinelWidget.Instance;

    public string Text
    {
        get => _text;
        set
        {
            _text = value ?? string.Empty;
            _label.Text = _text;
        }
    }

    public int MinWidth { get; set; }

    public Style Style
    {
        get => _style;
        set
        {
            _style = value;
            SyncChrome();
        }
    }

    public BorderStyle BorderStyle
    {
        get => _borderStyle;
        set
        {
            _borderStyle = value;
            SyncChrome();
        }
    }

    public Color? ForegroundColor
    {
        get => _foregroundColor;
        set
        {
            _foregroundColor = value;
            SyncChrome();
        }
    }

    public Color? BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            _backgroundColor = value;
            SyncChrome();
        }
    }

    public Color? BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            SyncChrome();
        }
    }

    public Action? OnClick { get; set; }

    public Action? OnFocus { get; set; }

    public Action? OnBlur { get; set; }

    public Action? OnMouseEnter { get; set; }

    public Action? OnMouseLeave { get; set; }

    public IWidgetRenderer GetRenderer() => _scribe;

    public IWidgetScribe GetScribe() => _scribe;

    public ILayoutCreator GetLayoutCreator() => new ButtonLayout();

    public void Activate() => OnClick?.Invoke();

    public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
    {
        visitor.Visit(_chrome);
    }

    public void Accept<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IVisitor, allows ref struct
    {
        visitor.Visit(_chrome);
    }

    void IEventHandler<OnFocus>.Handle(IEventContext context, in OnFocus e)
    {
        _ = e;
        OnFocus?.Invoke();
        context.RaiseEvent(new OnContentChanged());
        context.IsHandled = true;
    }

    void IEventHandler<OnBlur>.Handle(IEventContext context, in OnBlur e)
    {
        _ = e;
        OnBlur?.Invoke();
        context.RaiseEvent(new OnContentChanged());
        context.IsHandled = true;
    }

    void IEventHandler<OnMouseClick>.Handle(IEventContext context, in OnMouseClick e)
    {
        _ = e;
        Activate();
        context.IsHandled = true;
    }

    void IEventHandler<OnMouseEnter>.Handle(IEventContext context, in OnMouseEnter e)
    {
        _ = e;
        OnMouseEnter?.Invoke();
        context.RaiseEvent(new OnContentChanged());
        context.IsHandled = true;
    }

    void IEventHandler<OnMouseLeave>.Handle(IEventContext context, in OnMouseLeave e)
    {
        _ = e;
        OnMouseLeave?.Invoke();
        context.RaiseEvent(new OnContentChanged());
        context.IsHandled = true;
    }

    void SyncChrome()
    {
        var foreground = _foregroundColor ?? _style.Foreground;
        var background = _backgroundColor ?? _style.Background;

        _label.ForegroundColor = foreground;
        _label.BackgroundColor = background;
        _chrome.BorderStyle = _borderStyle;
        _chrome.Style = _style with
        {
            Foreground = foreground,
            Background = background
        };
        _chrome.BorderColor = _borderColor;
    }

}
