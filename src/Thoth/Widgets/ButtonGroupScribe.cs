using Thoth.Rendering;
using Thoth.Rendering.Layout;

namespace Thoth.Widgets;

public sealed class ButtonGroupScribe : IWidgetRenderer, IWidgetScribe
{
    readonly ButtonGroup _widget;
    int _measuredMaxWidth;
    int _measuredMaxHeight;
    int _measuredCount;
    int _measuredGap;
    Button? _measuredDefault;
    readonly List<measured_button> _measuredButtons = [];
    readonly List<Canvas.ChildPlacement> _childPlacements = [];

    public ButtonGroupScribe(ButtonGroup widget)
    {
        _widget = widget;
        _measuredMaxWidth = -1;
        _measuredMaxHeight = -1;
        _measuredCount = -1;
        _measuredGap = -1;
    }

    public Size Measure(SizeConstraint constraint)
    {
        EnsureMeasured(constraint);
        var gap = Math.Max(0, _widget.ButtonGap);
        var totalWidth = 0;
        var height = 0;
        for (var i = 0; i < _measuredButtons.Count; i++)
        {
            totalWidth += _measuredButtons[i].Size.Width;
            if (i > 0) totalWidth += gap;
            if (_measuredButtons[i].Size.Height > height) height = _measuredButtons[i].Size.Height;
        }
        return new(Math.Min(totalWidth, constraint.MaxWidth), Math.Min(height, constraint.MaxHeight));
    }

    public void Arrange(Rect rect)
    {
        EnsureMeasured(new(rect.Width, rect.Height));
        _widget.EnsureSelection();
        _widget.ApplyVisualSelection();
        _childPlacements.Clear();

        var gap = Math.Max(0, _widget.ButtonGap);
        var x = 0;
        for (var i = 0; i < _measuredButtons.Count; i++)
        {
            var measured = _measuredButtons[i];
            var available = Math.Max(0, rect.Width - x);
            var width = Math.Min(measured.Size.Width, available);
            var height = Math.Min(measured.Size.Height, rect.Height);
            var childRect = new Rect(x, 0, width, height);
            measured.Button.GetRenderer().Arrange(childRect);
            _childPlacements.Add(new(measured.Button, childRect));
            x += width + gap;
        }
    }

    public void Draw(Canvas canvas)
    {
        for (var i = 0; i < _childPlacements.Count; i++)
        {
            var placement = _childPlacements[i];
            canvas.RenderChild(_widget, in placement);
        }
    }

    public void InvalidateMeasured()
    {
        _measuredMaxWidth = -1;
        _measuredMaxHeight = -1;
        _measuredCount = -1;
        _measuredGap = -1;
        _measuredDefault = null;
        _measuredButtons.Clear();
    }

    void EnsureMeasured(SizeConstraint constraint)
    {
        var gap = Math.Max(0, _widget.ButtonGap);
        if (_measuredMaxWidth == constraint.MaxWidth &&
            _measuredMaxHeight == constraint.MaxHeight &&
            _measuredCount == _widget.ButtonCount &&
            _measuredGap == gap &&
            ReferenceEquals(_measuredDefault, _widget.DefaultButton))
            return;

        var ordered = _widget.EnsureOrdered();
        _measuredButtons.Clear();

        var totalWidth = 0;
        var height = 0;
        for (var i = 0; i < ordered.Count; i++)
        {
            var button = ordered[i];
            var size = button.GetRenderer().Measure(constraint);
            _measuredButtons.Add(new(button, size));
            totalWidth += size.Width;
            if (i > 0) totalWidth += gap;
            if (size.Height > height) height = size.Height;
        }

        _measuredMaxWidth = constraint.MaxWidth;
        _measuredMaxHeight = constraint.MaxHeight;
        _measuredCount = _widget.ButtonCount;
        _measuredGap = gap;
        _measuredDefault = _widget.DefaultButton;
    }

    readonly struct measured_button(Button button, Size size)
    {
        public readonly Button Button = button;
        public readonly Size Size = size;
    }
}
