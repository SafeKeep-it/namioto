using Thoth.Rendering;
using Thoth.Rendering.Layout;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public class ProgressBarLayout : ILayoutCreator
{
    public WidgetSizeRequest Measure(in IWidgetWithLayout widget,
                                     in SizeConstraint constraint,
                                     ReadOnlySpan<WidgetSizeRequest> desires)
    {
        var progressBar = widget as ProgressBar
            ?? throw new InvalidOperationException($"{nameof(ProgressBarLayout)} requires {nameof(ProgressBar)}.");

        _ = desires;
        var width = Math.Min(Math.Max(0, progressBar.Width), constraint.MaxWidth);
        var size = new Size(width, Math.Min(1, constraint.MaxHeight));
        return new(progressBar, this, size);
    }

    public void Arrange(in IWidgetWithLayout widget, in WidgetSize actual, ReadOnlySpan<WidgetSizeRequest> childDesires, Span<WidgetSize> children)
    {
        _ = widget;
        _ = actual;
        _ = childDesires;
        _ = children;
    }

    public void Draw(in IWidgetWithLayout widget, in Canvas canvas)
    {
        var progressBar = widget as ProgressBar
            ?? throw new InvalidOperationException($"{nameof(ProgressBarLayout)} requires {nameof(ProgressBar)}.");

        if (canvas.Width <= 0 || canvas.Height <= 0) return;

        var width = Math.Min(Math.Max(0, progressBar.Width), canvas.Width);
        if (width <= 0) return;

        var clampedProgress = Math.Clamp(progressBar.Progress, 0d, 1d);
        var fillCells = (int)Math.Round(clampedProgress * width, MidpointRounding.AwayFromZero);
        fillCells = Math.Clamp(fillCells, 0, width);

        var trackStyleIndex = canvas.Context.Styles.Intern(new Style(progressBar.TrackColor, null));
        var fillStyleIndex = canvas.Context.Styles.Intern(new Style(progressBar.FillColor, null));
        var trackGlyph = canvas.PrepareRune(progressBar.TrackGlyph);
        var fillGlyph = canvas.PrepareRune(progressBar.FillGlyph);

        canvas.FillPreparedGlyph(0, 0, width, 1, trackGlyph, trackStyleIndex);
        if (fillCells > 0)
            canvas.FillPreparedGlyph(0, 0, fillCells, 1, fillGlyph, fillStyleIndex);
    }
}
