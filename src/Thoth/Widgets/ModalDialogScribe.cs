using Thoth.Rendering;
using Thoth.Rendering.Layout;

namespace Thoth.Widgets;

public sealed class ModalDialogScribe : IWidgetRenderer, IWidgetScribe
{
    readonly ModalDialog _widget;
    Canvas.ChildPlacement _chromePlacement;
    bool _hasChrome;

    public ModalDialogScribe(ModalDialog widget)
    {
        _widget = widget;
    }

    public Size Measure(SizeConstraint constraint)
    {
        var width = clamp_dimension(_widget.Width, constraint.MaxWidth, _widget.MaxWidthRatio);
        var height = clamp_dimension(_widget.Height, constraint.MaxHeight, _widget.MaxHeightRatio);
        return new(width, height);
    }

    public void Arrange(Rect rect)
    {
        var width = clamp_dimension(_widget.Width, rect.Width, _widget.MaxWidthRatio);
        var height = clamp_dimension(_widget.Height, rect.Height, _widget.MaxHeightRatio);

        var x = Math.Max(0, (rect.Width - width) / 2);
        var y = Math.Max(0, (rect.Height - height) / 2);

        var chromeRect = new Rect(x, y, width, height);
        _widget.Chrome.GetRenderer().Arrange(chromeRect);
        _chromePlacement = new(_widget.Chrome, chromeRect);
        _hasChrome = true;
    }

    public void Draw(Canvas canvas)
    {
        if (!_hasChrome) return;
        canvas.RenderChild(_widget, in _chromePlacement);
    }

    static int clamp_dimension(int requested, int available, double ratio)
    {
        var normalizedRatio = Math.Clamp(ratio, 0.0, 1.0);
        var ratioLimit = Math.Max(1, (int)Math.Floor(available * normalizedRatio));
        var maxDimension = Math.Min(available, ratioLimit);
        return Math.Min(Math.Max(1, requested), maxDimension);
    }
}
