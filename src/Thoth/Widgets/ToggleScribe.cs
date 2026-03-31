using System.Text;
using Thoth.Rendering;
using Thoth.Rendering.Layout;

namespace Thoth.Widgets;

public sealed class ToggleScribe : IWidgetRenderer, IWidgetScribe
{
    readonly Toggle _widget;
    RenderContext? _cachedContext;
    Color? _checkedForeground;
    Color? _checkedBackground;
    int _checkedStyleIndex;
    bool _hasCheckedStyle;
    Color? _uncheckedForeground;
    Color? _uncheckedBackground;
    int _uncheckedStyleIndex;
    bool _hasUncheckedStyle;
    Rune _checkedGlyph;
    Canvas.PreparedGlyph _checkedPreparedGlyph;
    bool _hasCheckedGlyph;
    Rune _uncheckedGlyph;
    Canvas.PreparedGlyph _uncheckedPreparedGlyph;
    bool _hasUncheckedGlyph;

    public ToggleScribe(Toggle widget)
    {
        _widget = widget;
    }

    public Size Measure(SizeConstraint constraint)
    {
        return new(Math.Min(1, constraint.MaxWidth), Math.Min(1, constraint.MaxHeight));
    }

    public void Arrange(Rect rect)
    {
    }

    public void Draw(Canvas canvas)
    {
        EnsureCacheContext(canvas);
        if (canvas.Width <= 0 || canvas.Height <= 0) return;

        if (_widget.IsChecked)
        {
            var styleIndex = GetCheckedStyleIndex(_widget.CheckedForegroundColor, _widget.BackgroundColor, canvas);
            var glyph = GetCheckedGlyph(_widget.CheckedGlyph, canvas);
            canvas.PutPreparedGlyph(0, 0, glyph, styleIndex);
            return;
        }

        var uncheckedStyleIndex = GetUncheckedStyleIndex(_widget.UncheckedForegroundColor, _widget.BackgroundColor, canvas);
        var uncheckedGlyph = GetUncheckedGlyph(_widget.UncheckedGlyph, canvas);
        canvas.PutPreparedGlyph(0, 0, uncheckedGlyph, uncheckedStyleIndex);
    }

    void EnsureCacheContext(Canvas canvas)
    {
        if (ReferenceEquals(_cachedContext, canvas.Context)) return;
        _cachedContext = canvas.Context;
        _hasCheckedStyle = false;
        _hasUncheckedStyle = false;
        _hasCheckedGlyph = false;
        _hasUncheckedGlyph = false;
    }

    int GetCheckedStyleIndex(Color? foreground, Color? background, Canvas canvas)
    {
        if (!_hasCheckedStyle || _checkedForeground != foreground || _checkedBackground != background)
        {
            _checkedForeground = foreground;
            _checkedBackground = background;
            _checkedStyleIndex = canvas.Context.Styles.Intern(new Style(foreground, background));
            _hasCheckedStyle = true;
        }

        return _checkedStyleIndex;
    }

    int GetUncheckedStyleIndex(Color? foreground, Color? background, Canvas canvas)
    {
        if (!_hasUncheckedStyle || _uncheckedForeground != foreground || _uncheckedBackground != background)
        {
            _uncheckedForeground = foreground;
            _uncheckedBackground = background;
            _uncheckedStyleIndex = canvas.Context.Styles.Intern(new Style(foreground, background));
            _hasUncheckedStyle = true;
        }

        return _uncheckedStyleIndex;
    }

    Canvas.PreparedGlyph GetCheckedGlyph(Rune glyph, Canvas canvas)
    {
        if (!_hasCheckedGlyph || _checkedGlyph != glyph)
        {
            _checkedGlyph = glyph;
            _checkedPreparedGlyph = canvas.PrepareRune(glyph);
            _hasCheckedGlyph = true;
        }

        return _checkedPreparedGlyph;
    }

    Canvas.PreparedGlyph GetUncheckedGlyph(Rune glyph, Canvas canvas)
    {
        if (!_hasUncheckedGlyph || _uncheckedGlyph != glyph)
        {
            _uncheckedGlyph = glyph;
            _uncheckedPreparedGlyph = canvas.PrepareRune(glyph);
            _hasUncheckedGlyph = true;
        }

        return _uncheckedPreparedGlyph;
    }
}
