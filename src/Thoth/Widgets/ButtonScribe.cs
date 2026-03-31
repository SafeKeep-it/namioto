using Thoth.Rendering;
using Thoth.Rendering.Layout;

namespace Thoth.Widgets;

public sealed class ButtonScribe : IWidgetRenderer, IWidgetScribe
{
    readonly Button _widget;
    Canvas.ChildPlacement _chromePlacement;

    public ButtonScribe(Button widget)
    {
        _widget = widget;
    }

    public Size Measure(SizeConstraint constraint)
    {
        ApplyPalette();

        var autoSize = _widget._chrome.GetRenderer().Measure(constraint);
        var minWidth = Math.Max(0, _widget.MinWidth);
        var width = Math.Max(autoSize.Width, minWidth);
        return new(Math.Min(width, constraint.MaxWidth), autoSize.Height);
    }

    public void Arrange(Rect rect)
    {
        ApplyPalette();
        var chromeRect = new Rect(0, 0, rect.Width, rect.Height);
        _widget._chrome.GetRenderer().Arrange(chromeRect);
        _chromePlacement = new(_widget._chrome, chromeRect);
    }

    public void Draw(Canvas canvas)
    {
        ApplyPalette();
        canvas.RenderChild(_widget, in _chromePlacement);
    }

    void ApplyPalette()
    {
        var foreground = _widget.ForegroundColor ?? _widget.Style.Foreground;
        var background = _widget.BackgroundColor ?? _widget.Style.Background;

        _widget._label.ForegroundColor = foreground;
        _widget._label.BackgroundColor = background;
        _widget._chrome.BorderStyle = _widget.BorderStyle;
        _widget._chrome.Style = _widget.Style with
        {
            Foreground = foreground,
            Background = background
        };
        _widget._chrome.BorderColor = _widget.BorderColor;
    }
}
