using System.Text;
using Thoth.Rendering;
using Thoth.Rendering.Layout;

namespace Thoth.Widgets;

public sealed class TextBarScribe : IWidgetRenderer, IWidgetScribe
{
    readonly TextBar _widget;
    title_cache _leftTitle;
    title_cache _centerTitle;
    title_cache _rightTitle;
    RenderContext? _cachedContext;
    int _defaultStyleIndex;
    bool _hasDefaultStyle;
    Style _cachedDefaultStyle;
    int _rightStyleIndex;
    bool _hasRightStyle;
    Style _cachedRightStyle;

    public TextBarScribe(TextBar widget)
    {
        _widget = widget;
    }

    public Size Measure(SizeConstraint constraint)
    {
        return new(constraint.MaxWidth, 1);
    }

    public void Arrange(Rect rect)
    {
    }

    public void Draw(Canvas canvas)
    {
        var maxWidth = canvas.Width;
        if (maxWidth <= 0) return;

        var lineRune = (Rune)_widget.Line[0];

        var left = _widget.LeftTitle ?? string.Empty;
        var center = _widget.CenterTitle ?? string.Empty;
        var right = _widget.RightTitle ?? string.Empty;

        RefreshTitleCache(ref _leftTitle, left, canvas);
        RefreshTitleCache(ref _centerTitle, center, canvas);
        RefreshTitleCache(ref _rightTitle, right, canvas);

        var leftUtf8 = _leftTitle.Utf8Bytes ?? [];
        var centerUtf8 = _centerTitle.Utf8Bytes ?? [];
        var rightUtf8 = _rightTitle.Utf8Bytes ?? [];

        var leftLen = leftUtf8.Length;
        var centerLen = centerUtf8.Length;
        var rightLen = rightUtf8.Length;

        var leftWidth = _leftTitle.Width;
        var centerWidth = _centerTitle.Width;
        var rightWidth = _rightTitle.Width;

        var rightColumnEnd = maxWidth;
        var statusStart = Math.Max(0, rightColumnEnd - 1 - rightWidth);
        var titleStart = Math.Max(0, (maxWidth - centerWidth) / 2);

        var adjustedTitleStart = titleStart;
        if (adjustedTitleStart + centerWidth > statusStart)
            adjustedTitleStart = Math.Max(0, statusStart - centerWidth);
        if (adjustedTitleStart < leftWidth) adjustedTitleStart = leftWidth;

        EnsureCacheContext(canvas);
        var defaultStyleIndex = ResolveDefaultStyleIndex(_widget.Style, canvas);
        var lineGlyph = canvas.PrepareRune(lineRune);
        canvas.FillPreparedGlyph(0, 0, maxWidth, 1, lineGlyph, defaultStyleIndex);

        if (leftLen > 0)
            canvas.DrawUtf8ClippedWithStyleIndex(0, 0, leftUtf8, defaultStyleIndex, maxWidth);

        if (centerLen > 0)
        {
            var availableCenterWidth = statusStart - adjustedTitleStart;
            if (availableCenterWidth > 0)
                canvas.DrawUtf8ClippedWithStyleIndex(adjustedTitleStart, 0, centerUtf8, defaultStyleIndex, availableCenterWidth);
        }

        if (rightLen > 0)
        {
            var rightStyle = _widget.RightTitleStyle ?? _widget.Style;
            var rightStyleIndex = ResolveRightStyleIndex(rightStyle, canvas);
            canvas.DrawUtf8ClippedWithStyleIndex(statusStart, 0, rightUtf8, rightStyleIndex, Math.Max(0, maxWidth - statusStart));
        }
    }

    static void RefreshTitleCache(ref title_cache cache, string text, Canvas canvas)
    {
        if (string.Equals(cache.SourceText, text, StringComparison.Ordinal)) return;
        cache.SourceText = text;
        cache.Utf8Bytes = Encoding.UTF8.GetBytes(text);
        cache.Width = canvas.MeasureUtf8Width(cache.Utf8Bytes);
    }

    void EnsureCacheContext(Canvas canvas)
    {
        if (ReferenceEquals(_cachedContext, canvas.Context)) return;
        _cachedContext = canvas.Context;
        _hasDefaultStyle = false;
        _hasRightStyle = false;
    }

    int ResolveDefaultStyleIndex(Style style, Canvas canvas)
    {
        if (!_hasDefaultStyle || _cachedDefaultStyle != style)
        {
            _cachedDefaultStyle = style;
            _defaultStyleIndex = canvas.Context.Styles.Intern(style);
            _hasDefaultStyle = true;
        }

        return _defaultStyleIndex;
    }

    int ResolveRightStyleIndex(Style style, Canvas canvas)
    {
        if (!_hasRightStyle || _cachedRightStyle != style)
        {
            _cachedRightStyle = style;
            _rightStyleIndex = canvas.Context.Styles.Intern(style);
            _hasRightStyle = true;
        }

        return _rightStyleIndex;
    }

    struct title_cache
    {
        public string? SourceText;
        public byte[]? Utf8Bytes;
        public int Width;
    }
}
