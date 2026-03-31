using System.Text;
using Thoth.Rendering;
using Thoth.Rendering.Layout;

namespace Thoth.Widgets;

public sealed class BorderScribe : IWidgetRenderer, IWidgetScribe
{
    readonly Border _widget;
    Canvas.ChildPlacement _childPlacement;
    RenderContext? _cachedContext;
    Color? _cachedBackgroundColor;
    int _backgroundStyleIndex;
    bool _hasBackgroundStyle;
    Canvas.PreparedGlyph _spaceGlyph;
    bool _hasSpaceGlyph;
    Style _strokeBaseStyle;
    Color? _strokeForeground;
    Color? _strokeBackground;
    int _strokeStyleIndex;
    bool _hasStrokeStyle;
    BorderStyle _cachedBorderStyle;
    border_glyphs _borderGlyphs;
    bool _hasBorderGlyphs;

    public BorderScribe(Border widget)
    {
        _widget = widget;
    }

    public Size Measure(SizeConstraint constraint)
    {
        var hasChrome = _widget.BorderStyle != BorderStyle.None;
        var chromePad = hasChrome ? 2 : 0;
        var innerConstraint = new SizeConstraint(Math.Max(0, constraint.MaxWidth - chromePad),
                                                 Math.Max(0, constraint.MaxHeight - chromePad));
        var innerSize = _widget.Content.GetRenderer().Measure(innerConstraint);
        return new(innerSize.Width + chromePad, innerSize.Height + chromePad);
    }

    public void Arrange(Rect rect)
    {
        var hasChrome = _widget.BorderStyle != BorderStyle.None;
        var offset = hasChrome ? 1 : 0;
        var chromePad = hasChrome ? 2 : 0;
        var childRect = new Rect(offset, offset, Math.Max(0, rect.Width - chromePad), Math.Max(0, rect.Height - chromePad));
        _widget.Content.GetRenderer().Arrange(childRect);
        _childPlacement = new(_widget.Content, childRect);
    }

    public void Draw(Canvas canvas)
    {
        EnsureCacheContext(canvas);

        var background = EffectiveBackgroundColor;
        var hasBackgroundStyle = false;
        var backgroundStyleIndex = 0;
        if (background is { } bg)
        {
            backgroundStyleIndex = ResolveBackgroundStyleIndex(bg, canvas);
            hasBackgroundStyle = true;
        }

        if (_widget.BorderStyle == BorderStyle.None)
        {
            if (hasBackgroundStyle)
                PaintBackground(backgroundStyleIndex, canvas);
            canvas.RenderChild(_widget, in _childPlacement);
            return;
        }

        if (canvas.Width < 2 || canvas.Height < 2) return;

        if (_widget.BorderStyle == BorderStyle.Inset && _widget.Labels.HasAny)
            throw new InvalidOperationException("Border labels are not supported with BorderStyle.Inset.");

        var foreground = EffectiveBorderColor;
        var strokeBackground = _widget.BorderStyle is BorderStyle.Outline or BorderStyle.Rounded
            ? background ?? _widget.Style.Background
            : _widget.Style.Background;
        var strokeStyleIndex = ResolveStrokeStyleIndex(_widget.Style, foreground, strokeBackground, canvas);

        if (hasBackgroundStyle)
            PaintBackground(backgroundStyleIndex, canvas);
        DrawBox(canvas, strokeStyleIndex);
        DrawLabels(canvas, strokeStyleIndex);
        canvas.RenderChild(_widget, in _childPlacement);
    }

    void EnsureCacheContext(Canvas canvas)
    {
        if (ReferenceEquals(_cachedContext, canvas.Context)) return;
        _cachedContext = canvas.Context;
        _hasBackgroundStyle = false;
        _hasStrokeStyle = false;
        _hasSpaceGlyph = false;
        _hasBorderGlyphs = false;
    }

    void PaintBackground(int backgroundStyleIndex, Canvas canvas)
    {
        var spaceGlyph = GetSpaceGlyph(canvas);
        if (_widget.BorderStyle is BorderStyle.Single or BorderStyle.Inset)
        {
            if (canvas.Width > 2 && canvas.Height > 2)
                canvas.FillPreparedGlyph(1, 1, canvas.Width - 2, canvas.Height - 2, spaceGlyph, backgroundStyleIndex);
            return;
        }

        canvas.FillPreparedGlyph(0, 0, canvas.Width, canvas.Height, spaceGlyph, backgroundStyleIndex);
    }

    int ResolveBackgroundStyleIndex(Color background, Canvas canvas)
    {
        if (!_hasBackgroundStyle || _cachedBackgroundColor != background)
        {
            _cachedBackgroundColor = background;
            _backgroundStyleIndex = canvas.Context.Styles.Intern(new Style(Background: background));
            _hasBackgroundStyle = true;
        }

        return _backgroundStyleIndex;
    }

    Canvas.PreparedGlyph GetSpaceGlyph(Canvas canvas)
    {
        if (!_hasSpaceGlyph)
        {
            _spaceGlyph = canvas.PrepareRune((Rune)' ');
            _hasSpaceGlyph = true;
        }

        return _spaceGlyph;
    }

    void DrawBox(Canvas canvas, int strokeStyleIndex)
    {
        var w = canvas.Width;
        var h = canvas.Height;
        var glyphs = GetBorderGlyphs(_widget.BorderStyle, canvas);

        canvas.PutPreparedGlyph(0, 0, glyphs.TopLeft, strokeStyleIndex);
        canvas.PutPreparedGlyph(w - 1, 0, glyphs.TopRight, strokeStyleIndex);
        canvas.PutPreparedGlyph(0, h - 1, glyphs.BottomLeft, strokeStyleIndex);
        canvas.PutPreparedGlyph(w - 1, h - 1, glyphs.BottomRight, strokeStyleIndex);

        if (w > 2)
        {
            canvas.FillPreparedGlyph(1, 0, w - 2, 1, glyphs.Top, strokeStyleIndex);
            canvas.FillPreparedGlyph(1, h - 1, w - 2, 1, glyphs.Bottom, strokeStyleIndex);
        }

        if (h > 2)
        {
            canvas.FillPreparedGlyph(0, 1, 1, h - 2, glyphs.Left, strokeStyleIndex);
            canvas.FillPreparedGlyph(w - 1, 1, 1, h - 2, glyphs.Right, strokeStyleIndex);
        }
    }

    int ResolveStrokeStyleIndex(Style baseStyle, Color? foreground, Color? background, Canvas canvas)
    {
        if (!_hasStrokeStyle
            || _strokeBaseStyle != baseStyle
            || _strokeForeground != foreground
            || _strokeBackground != background)
        {
            _strokeBaseStyle = baseStyle;
            _strokeForeground = foreground;
            _strokeBackground = background;
            var style = baseStyle with { Foreground = foreground, Background = background };
            _strokeStyleIndex = canvas.Context.Styles.Intern(style);
            _hasStrokeStyle = true;
        }

        return _strokeStyleIndex;
    }

    border_glyphs GetBorderGlyphs(BorderStyle borderStyle, Canvas canvas)
    {
        if (!_hasBorderGlyphs || _cachedBorderStyle != borderStyle)
        {
            _cachedBorderStyle = borderStyle;
            var (tl, tr, bl, br, hzTop, hzBottom, vtLeft, vtRight) = ResolveBorderRunes(borderStyle);
            _borderGlyphs = new(canvas.PrepareRune(tl),
                                canvas.PrepareRune(tr),
                                canvas.PrepareRune(bl),
                                canvas.PrepareRune(br),
                                canvas.PrepareRune(hzTop),
                                canvas.PrepareRune(hzBottom),
                                canvas.PrepareRune(vtLeft),
                                canvas.PrepareRune(vtRight));
            _hasBorderGlyphs = true;
        }

        return _borderGlyphs;
    }

    void DrawLabels(Canvas canvas, int strokeStyleIndex)
    {
        var available = canvas.Width - 2;
        if (available <= 0) return;

        var topCenter = _widget.Labels.TopCenter;
        if (!string.IsNullOrWhiteSpace(topCenter))
        {
            var text = topCenter.Length > available ? topCenter[..available] : topCenter;
            var startX = 1 + Math.Max(0, (available - text.Length) / 2);
            canvas.DrawStringWithStyleIndex(startX, 0, text, strokeStyleIndex);
        }

        var bottomLeft = _widget.Labels.BottomLeft;
        if (!string.IsNullOrWhiteSpace(bottomLeft))
        {
            var text = bottomLeft.Length > available ? bottomLeft[..available] : bottomLeft;
            canvas.DrawStringWithStyleIndex(1, canvas.Height - 1, text, strokeStyleIndex);
        }
    }

    Color? EffectiveBorderColor =>
        _widget.IsHovered
            ? _widget.HoverBorderColor ?? LiftColor(_widget.BorderColor ?? _widget.Style.Foreground)
            : _widget.BorderColor ?? _widget.Style.Foreground;

    Color? EffectiveBackgroundColor =>
        _widget.IsHovered
            ? _widget.HoverBackgroundColor ?? LiftColor(_widget.BackgroundColor ?? _widget.Style.Background)
            : _widget.BackgroundColor ?? _widget.Style.Background;

    static Color? LiftColor(Color? color)
    {
        if (color is not { } c) return null;

        static byte add(byte v) => (byte)Math.Min(255, v + 32);
        return new(add(c.R), add(c.G), add(c.B));
    }

    static (Rune tl, Rune tr, Rune bl, Rune br, Rune hzTop, Rune hzBottom, Rune vtLeft, Rune vtRight)
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

    readonly record struct border_glyphs(
        Canvas.PreparedGlyph TopLeft,
        Canvas.PreparedGlyph TopRight,
        Canvas.PreparedGlyph BottomLeft,
        Canvas.PreparedGlyph BottomRight,
        Canvas.PreparedGlyph Top,
        Canvas.PreparedGlyph Bottom,
        Canvas.PreparedGlyph Left,
        Canvas.PreparedGlyph Right);
}
