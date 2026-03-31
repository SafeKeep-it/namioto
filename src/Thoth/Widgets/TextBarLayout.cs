using System.Text;
using Thoth.Rendering;
using Thoth.Rendering.Layout;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public class TextBarLayout : ILayoutCreator
{
    public WidgetSizeRequest Measure(in IWidgetWithLayout widget,
                                     in SizeConstraint constraint,
                                     ReadOnlySpan<WidgetSizeRequest> desires)
    {
        var textBar = widget as TextBar
            ?? throw new InvalidOperationException($"{nameof(TextBarLayout)} requires {nameof(TextBar)}.");

        _ = desires;
        var size = new Size(constraint.MaxWidth, 1);
        return new(textBar, this, size);
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
        var textBar = widget as TextBar
            ?? throw new InvalidOperationException($"{nameof(TextBarLayout)} requires {nameof(TextBar)}.");

        var maxWidth = canvas.Width;
        if (maxWidth <= 0) return;

        var lineRune = (Rune)textBar.Line[0];

        var left = textBar.LeftTitle ?? string.Empty;
        var center = textBar.CenterTitle ?? string.Empty;
        var right = textBar.RightTitle ?? string.Empty;

        var leftUtf8 = Encoding.UTF8.GetBytes(left);
        var centerUtf8 = Encoding.UTF8.GetBytes(center);
        var rightUtf8 = Encoding.UTF8.GetBytes(right);

        var leftWidth = canvas.MeasureUtf8Width(leftUtf8);
        var centerWidth = canvas.MeasureUtf8Width(centerUtf8);
        var rightWidth = canvas.MeasureUtf8Width(rightUtf8);

        var rightColumnEnd = maxWidth;
        var statusStart = Math.Max(0, rightColumnEnd - 1 - rightWidth);
        var titleStart = Math.Max(0, (maxWidth - centerWidth) / 2);

        var adjustedTitleStart = titleStart;
        if (adjustedTitleStart + centerWidth > statusStart)
            adjustedTitleStart = Math.Max(0, statusStart - centerWidth);
        if (adjustedTitleStart < leftWidth) adjustedTitleStart = leftWidth;

        var defaultStyleIndex = canvas.Context.Styles.Intern(textBar.Style);
        var lineGlyph = canvas.PrepareRune(lineRune);
        canvas.FillPreparedGlyph(0, 0, maxWidth, 1, lineGlyph, defaultStyleIndex);

        if (leftUtf8.Length > 0)
            canvas.DrawUtf8ClippedWithStyleIndex(0, 0, leftUtf8, defaultStyleIndex, maxWidth);

        if (centerUtf8.Length > 0)
        {
            var availableCenterWidth = statusStart - adjustedTitleStart;
            if (availableCenterWidth > 0)
                canvas.DrawUtf8ClippedWithStyleIndex(adjustedTitleStart, 0, centerUtf8, defaultStyleIndex, availableCenterWidth);
        }

        if (rightUtf8.Length > 0)
        {
            var rightStyleIndex = canvas.Context.Styles.Intern(textBar.RightTitleStyle ?? textBar.Style);
            canvas.DrawUtf8ClippedWithStyleIndex(statusStart,
                                                 0,
                                                 rightUtf8,
                                                 rightStyleIndex,
                                                 Math.Max(0, maxWidth - statusStart));
        }
    }
}
