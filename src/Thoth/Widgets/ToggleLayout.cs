using Thoth.Rendering;
using Thoth.Rendering.Layout;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public class ToggleLayout : ILayoutCreator
{
    public WidgetSizeRequest Measure(in IWidgetWithLayout widget,
                                     in SizeConstraint constraint,
                                     ReadOnlySpan<WidgetSizeRequest> desires)
    {
        var toggle = widget as Toggle
            ?? throw new InvalidOperationException($"{nameof(ToggleLayout)} requires {nameof(Toggle)}.");

        _ = desires;
        var size = new Size(Math.Min(1, constraint.MaxWidth), Math.Min(1, constraint.MaxHeight));
        return new(toggle, this, size);
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
        var toggle = widget as Toggle
            ?? throw new InvalidOperationException($"{nameof(ToggleLayout)} requires {nameof(Toggle)}.");

        if (canvas.Width <= 0 || canvas.Height <= 0) return;

        if (toggle.IsChecked)
        {
            var styleIndex = canvas.Context.Styles.Intern(new Style(toggle.CheckedForegroundColor, toggle.BackgroundColor));
            var glyph = canvas.PrepareRune(toggle.CheckedGlyph);
            canvas.PutPreparedGlyph(0, 0, glyph, styleIndex);
            return;
        }

        var uncheckedStyleIndex = canvas.Context.Styles.Intern(new Style(toggle.UncheckedForegroundColor, toggle.BackgroundColor));
        var uncheckedGlyph = canvas.PrepareRune(toggle.UncheckedGlyph);
        canvas.PutPreparedGlyph(0, 0, uncheckedGlyph, uncheckedStyleIndex);
    }
}
