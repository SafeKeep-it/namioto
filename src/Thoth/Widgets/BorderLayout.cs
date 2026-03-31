using System.Text;
using Thoth.Rendering;
using Thoth.Rendering.Layout;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public class BorderLayout : ILayoutCreator
{
    public WidgetSizeRequest Measure(in IWidgetWithLayout widget,
                                     in SizeConstraint constraint,
                                     ReadOnlySpan<WidgetSizeRequest> requests)
    {
        var border = widget as Border
            ?? throw new InvalidOperationException($"{nameof(BorderLayout)} requires {nameof(Border)}.");

        var hasChrome = border.BorderStyle != BorderStyle.None;
        var chromePad = hasChrome ? 2 : 0;
        var requested = requests[0].Size;
        var size = new Size(requested.Width + chromePad, requested.Height + chromePad);
        return new(widget, this, size);
    }

    public void Arrange(in IWidgetWithLayout widget, in WidgetSize actual, ReadOnlySpan<WidgetSizeRequest> childRequests, Span<WidgetSize> children)
    {
        var border = widget as Border
            ?? throw new InvalidOperationException($"{nameof(BorderLayout)} requires {nameof(Border)}.");
        var child = border.Content as IWidgetWithLayout
            ?? throw new InvalidOperationException($"{nameof(Border)} content must implement {nameof(IWidgetWithLayout)}.");

        var hasChrome = border.BorderStyle != BorderStyle.None;
        var offset = hasChrome ? 1 : 0;
        var chromePad = hasChrome ? 2 : 0;

        children[0] = new WidgetSize(child,
                                     child.GetLayoutCreator(),
                                     new Rect(offset,
                                              offset,
                                              Math.Max(0, actual.Rect.Width - chromePad),
                                              Math.Max(0, actual.Rect.Height - chromePad)));
    }

    public void Draw(in IWidgetWithLayout widget, in Canvas canvas)
    {
        var border = widget as Border
            ?? throw new InvalidOperationException($"{nameof(BorderLayout)} requires {nameof(Border)}.");

        var background = EffectiveBackgroundColor(border);
        if (background is { } bg)
            PaintBackground(border, bg, canvas);

        if (border.BorderStyle == BorderStyle.None)
            return;

        if (canvas.Width < 2 || canvas.Height < 2)
            return;

        if (border.BorderStyle == BorderStyle.Inset && border.Labels.HasAny)
            throw new InvalidOperationException("Border labels are not supported with BorderStyle.Inset.");

        var foreground = EffectiveBorderColor(border);
        var strokeBackground = border.BorderStyle is BorderStyle.Outline or BorderStyle.Rounded
            ? background ?? border.Style.Background
            : border.Style.Background;
        var strokeStyleIndex = canvas.Context.Styles.Intern(border.Style with
        {
            Foreground = foreground,
            Background = strokeBackground
        });

        DrawBox(border, canvas, strokeStyleIndex);
        DrawLabels(border, canvas, strokeStyleIndex);
    }

    void PaintBackground(Border border, Color background, Canvas canvas)
    {
        var backgroundStyleIndex = canvas.Context.Styles.Intern(new Style(Background: background));
        var space = canvas.PrepareRune((Rune)' ');

        if (border.BorderStyle is BorderStyle.Single or BorderStyle.Inset)
        {
            if (canvas.Width > 2 && canvas.Height > 2)
                canvas.FillPreparedGlyph(1, 1, canvas.Width - 2, canvas.Height - 2, space, backgroundStyleIndex);
            return;
        }

        canvas.FillPreparedGlyph(0, 0, canvas.Width, canvas.Height, space, backgroundStyleIndex);
    }

    void DrawBox(Border border, Canvas canvas, int strokeStyleIndex)
    {
        var (tl, tr, bl, br, hzTop, hzBottom, vtLeft, vtRight) = ResolveBorderRunes(border.BorderStyle);
        var topLeft = canvas.PrepareRune(tl);
        var topRight = canvas.PrepareRune(tr);
        var bottomLeft = canvas.PrepareRune(bl);
        var bottomRight = canvas.PrepareRune(br);
        var top = canvas.PrepareRune(hzTop);
        var bottom = canvas.PrepareRune(hzBottom);
        var left = canvas.PrepareRune(vtLeft);
        var right = canvas.PrepareRune(vtRight);

        var w = canvas.Width;
        var h = canvas.Height;

        canvas.PutPreparedGlyph(0, 0, topLeft, strokeStyleIndex);
        canvas.PutPreparedGlyph(w - 1, 0, topRight, strokeStyleIndex);
        canvas.PutPreparedGlyph(0, h - 1, bottomLeft, strokeStyleIndex);
        canvas.PutPreparedGlyph(w - 1, h - 1, bottomRight, strokeStyleIndex);

        if (w > 2)
        {
            canvas.FillPreparedGlyph(1, 0, w - 2, 1, top, strokeStyleIndex);
            canvas.FillPreparedGlyph(1, h - 1, w - 2, 1, bottom, strokeStyleIndex);
        }

        if (h > 2)
        {
            canvas.FillPreparedGlyph(0, 1, 1, h - 2, left, strokeStyleIndex);
            canvas.FillPreparedGlyph(w - 1, 1, 1, h - 2, right, strokeStyleIndex);
        }
    }

    void DrawLabels(Border border, Canvas canvas, int strokeStyleIndex)
    {
        var available = canvas.Width - 2;
        if (available <= 0)
            return;

        var topCenter = border.Labels.TopCenter;
        if (!string.IsNullOrWhiteSpace(topCenter))
        {
            var text = topCenter.Length > available ? topCenter[..available] : topCenter;
            var startX = 1 + Math.Max(0, (available - text.Length) / 2);
            canvas.DrawStringWithStyleIndex(startX, 0, text, strokeStyleIndex);
        }

        var bottomLeft = border.Labels.BottomLeft;
        if (!string.IsNullOrWhiteSpace(bottomLeft))
        {
            var text = bottomLeft.Length > available ? bottomLeft[..available] : bottomLeft;
            canvas.DrawStringWithStyleIndex(1, canvas.Height - 1, text, strokeStyleIndex);
        }
    }

    Color? EffectiveBorderColor(Border border)
    {
        return border.IsHovered
            ? border.HoverBorderColor ?? LiftColor(border.BorderColor ?? border.Style.Foreground)
            : border.BorderColor ?? border.Style.Foreground;
    }

    Color? EffectiveBackgroundColor(Border border)
    {
        return border.IsHovered
            ? border.HoverBackgroundColor ?? LiftColor(border.BackgroundColor ?? border.Style.Background)
            : border.BackgroundColor ?? border.Style.Background;
    }

    Color? LiftColor(Color? color)
    {
        if (color is not { } c)
            return null;

        byte Add(byte value) => (byte)Math.Min(255, value + 32);
        return new(Add(c.R), Add(c.G), Add(c.B));
    }

    (Rune tl, Rune tr, Rune bl, Rune br, Rune hzTop, Rune hzBottom, Rune vtLeft, Rune vtRight)
        ResolveBorderRunes(BorderStyle style)
    {
        return style switch
        {
            BorderStyle.Rounded =>
                ((Rune)'╭', (Rune)'╮', (Rune)'╰', (Rune)'╯', (Rune)'─', (Rune)'─', (Rune)'│', (Rune)'│'),
            BorderStyle.Outline =>
                ((Rune)'▛', (Rune)'▜', (Rune)'▙', (Rune)'▟', (Rune)'▔', (Rune)'▁', (Rune)'▏', (Rune)'▕'),
            BorderStyle.Inset =>
                ((Rune)'╔', (Rune)'╗', (Rune)'╚', (Rune)'╝', (Rune)'═', (Rune)'═', (Rune)'║', (Rune)'║'),
            _ =>
                ((Rune)'┌', (Rune)'┐', (Rune)'└', (Rune)'┘', (Rune)'─', (Rune)'─', (Rune)'│', (Rune)'│')
        };
    }
}
