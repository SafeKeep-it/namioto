using System.Runtime.InteropServices;
using System.Text;
using Thoth.Rendering;
using Thoth.Rendering.Layout;
using Thoth.Rendering.Text;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public sealed class TextBlockScribe : IWidgetRenderer, IWidgetScribe
{
    const int MaxCachedLayouts = 8;
    static readonly IWidthProvider DefaultWidthProvider = new UnicodeWidthProvider();

    readonly TextBlock _widget;
    int _measuredWidth = int.MinValue;
    int _maxLineWidth;
    int _measuredLineCount;
    readonly List<cached_layout> _cachedLayouts = [];
    readonly List<TextTokenizer.TextRun> _tokenRuns = [];
    int _tokenizedContentVersion = int.MinValue;
    TextTokenizer _tokenizer = new(DefaultWidthProvider);
    TextLayout _tokenLayout = new();
    long _nextCacheTick;
    int[] _cachedStyleIndices = [];
    ulong _cachedStyleSignature = ulong.MaxValue;
    int _cachedStyleRunCount = -1;
    Color? _cachedStyleForeground;
    Color? _cachedStyleBackground;
    object? _styleCacheCanvasContext;

    public TextBlockScribe(TextBlock widget)
    {
        _widget = widget;
    }

    public Size Measure(SizeConstraint constraint)
    {
        EnsureMeasured(_widget, constraint.MaxWidth);
        return new(_maxLineWidth, _measuredLineCount);
    }

    public void Arrange(Rect rect)
    {
        EnsureMeasured(_widget, rect.Width);
    }

    public void Draw(Canvas canvas)
    {
        if (_widget.BackgroundColor is { } bg)
            canvas.Fill(0, 0, canvas.Width, canvas.Height, new(' '), new(Background: bg));

        if (_widget.Overflow == TextOverflow.Marquee)
        {
            DrawTokenizedMarquee(_widget, canvas);
            return;
        }

        DrawTokenizedLines(_widget, canvas);
    }

    public void ClearMeasuredCache()
    {
        _measuredWidth = int.MinValue;
        _maxLineWidth = 0;
        _measuredLineCount = 0;
        _cachedLayouts.Clear();
        _tokenizedContentVersion = int.MinValue;
        _cachedStyleSignature = ulong.MaxValue;
        _cachedStyleRunCount = -1;
        _styleCacheCanvasContext = null;
        _nextCacheTick = 0;
    }

    void DrawTokenizedLines(TextBlock widget, Canvas canvas)
    {
        var styleIndices = EnsureStyleIndices(widget, canvas);
        var tokens = CollectionsMarshal.AsSpan(_tokenLayout.Tokens);
        var runs = CollectionsMarshal.AsSpan(_tokenRuns);
        var lines = _tokenLayout.Lines;
        var maxLines = Math.Min(lines.Count, canvas.Height);

        for (var lineIndex = 0; lineIndex < maxLines; lineIndex++)
        {
            var line = lines[lineIndex];
            var x = widget.Align == Align.Right ? canvas.Width - line.EstimatedWidth : 0;
            canvas.DrawTokenLine(x, lineIndex, tokens, line.TokenStart, line.TokenCount, runs, styleIndices);

            var isLastVisible = lineIndex == maxLines - 1;
            var hasOverflow = lineIndex < lines.Count - 1 || line.EstimatedWidth > canvas.Width;
            if (isLastVisible && hasOverflow && widget.Overflow == TextOverflow.Ellipsis && canvas.Width >= 1)
            {
                var lastRunIndex = line.TokenCount > 0
                    ? tokens[line.TokenStart + line.TokenCount - 1].RunIndex
                    : 0;
                var ellipsisStyleIndex = lastRunIndex < styleIndices.Length
                    ? styleIndices[lastRunIndex]
                    : styleIndices[0];
                var ellipsis = canvas.PrepareRune(new('\u2026'));
                canvas.PutPreparedGlyph(canvas.Width - 1, lineIndex, ellipsis, ellipsisStyleIndex);
            }
        }
    }

    void DrawTokenizedMarquee(TextBlock widget, Canvas canvas)
    {
        if (canvas.Height <= 0 || canvas.Width <= 0) return;

        var tokens = CollectionsMarshal.AsSpan(_tokenLayout.Tokens);
        var tokenCount = tokens.Length;
        if (tokenCount == 0) return;

        var totalWidth = 0;
        for (var i = 0; i < tokenCount; i++)
            totalWidth += tokens[i].EstimatedWidth;

        var styleIndices = EnsureStyleIndices(widget, canvas);
        var runs = CollectionsMarshal.AsSpan(_tokenRuns);

        if (totalWidth <= canvas.Width)
        {
            canvas.DrawTokenLine(0, 0, tokens, 0, tokenCount, runs, styleIndices);
            widget.SetMarqueeCycleWidth(1);
            return;
        }

        const int gapWidth = 3;
        var cycleWidth = totalWidth + gapWidth;
        widget.SetMarqueeCycleWidth(cycleWidth);
        var offset = widget.MarqueeOffset % cycleWidth;

        canvas.DrawTokenLine(-offset, 0, tokens, 0, tokenCount, runs, styleIndices);
        canvas.DrawTokenLine(cycleWidth - offset, 0, tokens, 0, tokenCount, runs, styleIndices);
    }

    ReadOnlySpan<int> EnsureStyleIndices(TextBlock widget, Canvas canvas)
    {
        var runCount = widget.Runs.Count;
        if (runCount == 0) return [];

        var styleSignature = ComputeStyleSignature(widget);
        var requiresRefresh = !ReferenceEquals(_styleCacheCanvasContext, canvas.Context)
            || _cachedStyleSignature != styleSignature
            || _cachedStyleForeground != widget.ForegroundColor
            || _cachedStyleBackground != widget.BackgroundColor
            || _cachedStyleRunCount != runCount;

        if (_cachedStyleIndices.Length < runCount)
        {
            Array.Resize(ref _cachedStyleIndices, runCount);
            requiresRefresh = true;
        }

        if (requiresRefresh)
        {
            for (var i = 0; i < runCount; i++)
                _cachedStyleIndices[i] = ResolveStyleIndex(widget, canvas, widget.Runs[i].StyleId, widget.Runs[i].LinkId);

            _cachedStyleSignature = styleSignature;
            _cachedStyleForeground = widget.ForegroundColor;
            _cachedStyleBackground = widget.BackgroundColor;
            _cachedStyleRunCount = runCount;
            _styleCacheCanvasContext = canvas.Context;
        }

        return _cachedStyleIndices.AsSpan(0, runCount);
    }

    static ulong ComputeStyleSignature(TextBlock widget)
    {
        const ulong offsetBasis = 1469598103934665603UL;
        const ulong prime = 1099511628211UL;

        var hash = offsetBasis;
        var runs = widget.Runs;
        for (var i = 0; i < runs.Count; i++)
        {
            var styleValue = runs[i].StyleId?.Value ?? int.MinValue;
            var linkValue = runs[i].LinkId?.Value ?? int.MinValue;
            hash ^= (ulong)(uint)styleValue;
            hash *= prime;
            hash ^= (ulong)(uint)linkValue;
            hash *= prime;
        }

        return hash;
    }

    static int ResolveStyleIndex(TextBlock widget, Canvas canvas, StyleId? styleId, LinkId? linkId)
    {
        var style = styleId.HasValue
            ? canvas.Context.Styles.Get(styleId.Value.Value)
            : new Style();

        if (style.Foreground == null && widget.ForegroundColor != null)
            style = style with { Foreground = widget.ForegroundColor };

        if (style.Background == null && widget.BackgroundColor != null)
            style = style with { Background = widget.BackgroundColor };

        if (linkId.HasValue)
            style = style with { Hyperlink = canvas.Context.Links.Get(linkId.Value.Value) };

        return canvas.Context.Styles.Intern(style);
    }

    void EnsureMeasured(TextBlock widget, int maxWidth)
    {
        if (_measuredWidth == maxWidth) return;

        for (var i = 0; i < _cachedLayouts.Count; i++)
        {
            var cached = _cachedLayouts[i];
            if (cached.Width != maxWidth) continue;

            _measuredWidth = cached.Width;
            _maxLineWidth = cached.MaxLineWidth;
            _tokenLayout.Reflow(maxWidth, widget.Overflow);
            _measuredLineCount = _tokenLayout.Lines.Count;

            cached.LastUsedTick = ++_nextCacheTick;
            _cachedLayouts[i] = cached;
            return;
        }

        Layout(widget, maxWidth);
    }

    void Layout(TextBlock widget, int maxWidth)
    {
        EnsureTokenized(widget, maxWidth);
        _tokenLayout.Reflow(maxWidth, widget.Overflow);
        var width = 0;
        for (var i = 0; i < _tokenLayout.Lines.Count; i++)
            width = Math.Max(width, _tokenLayout.Lines[i].EstimatedWidth);

        if (_cachedLayouts.Count >= MaxCachedLayouts)
        {
            var evictIndex = 0;
            var evictTick = _cachedLayouts[0].LastUsedTick;
            for (var i = 1; i < _cachedLayouts.Count; i++)
            {
                if (_cachedLayouts[i].LastUsedTick >= evictTick) continue;
                evictIndex = i;
                evictTick = _cachedLayouts[i].LastUsedTick;
            }

            _cachedLayouts.RemoveAt(evictIndex);
        }

        _cachedLayouts.Add(new(maxWidth, width, ++_nextCacheTick));
        _measuredWidth = maxWidth;
        _maxLineWidth = width;
        _measuredLineCount = _tokenLayout.Lines.Count;
    }

    void EnsureTokenized(TextBlock widget, int maxWidth)
    {
        var version = widget.ContentVersion;
        if (version == _tokenizedContentVersion) return;

        _tokenRuns.Clear();
        if (_tokenRuns.Capacity < widget.Runs.Count)
            _tokenRuns.Capacity = widget.Runs.Count;

        for (var i = 0; i < widget.Runs.Count; i++)
        {
            var utf8 = Encoding.UTF8.GetBytes(widget.Runs[i].Text);
            _tokenRuns.Add(new(utf8));
        }

        var tokenDelta = _tokenizer.Tokenize(_tokenRuns);
        _tokenLayout.Initialize(tokenDelta);
        _tokenizedContentVersion = version;
        _cachedStyleSignature = ulong.MaxValue;
    }

    struct cached_layout(int width, int maxLineWidth, long lastUsedTick)
    {
        public int Width { get; } = width;
        public int MaxLineWidth { get; } = maxLineWidth;
        public long LastUsedTick { get; set; } = lastUsedTick;
    }
}
