using System.Text;
using Thoth.Rendering.Grid;
using Thoth.Rendering.Text;
using Thoth.Widgets;

namespace Thoth.Rendering;

public readonly struct Canvas
{
    readonly GridBuffer buffer;
    readonly Rect bounds;
    readonly RenderContext context;
    readonly ushort frameNumber;
    readonly ICanvasChildRenderer? childRenderer;

    public readonly struct PreparedGlyph(int glyphId, byte width)
    {
        public int GlyphId { get; } = glyphId;
        public byte Width { get; } = width;
    }

    public readonly record struct ChildPlacement(IWidget Child, Rect Rect);

    public Canvas(GridBuffer buffer,
                  Rect bounds,
                  RenderContext context,
                  int offsetX = 0,
                  int offsetY = 0,
                  ushort frameNumber = 0)
        : this(buffer, bounds, context, offsetX, offsetY, frameNumber, null)
    {
    }

    internal Canvas(GridBuffer buffer,
                    Rect bounds,
                    RenderContext context,
                    int offsetX,
                    int offsetY,
                    ushort frameNumber,
                    ICanvasChildRenderer? childRenderer)
    {
        this.buffer = buffer;
        this.bounds = bounds;
        this.context = context;
        OffsetX = offsetX;
        OffsetY = offsetY;
        this.frameNumber = frameNumber;
        this.childRenderer = childRenderer;
    }

    public RenderContext Context => context;
    public int Width => bounds.Width;
    public int Height => bounds.Height;
    public int OffsetX { get; init; }
    public int OffsetY { get; init; }
    internal Rect Bounds => bounds;
    internal int RenderChildCallCount => childRenderer?.RenderChildCallCount ?? 0;

    public void RenderChild(IWidget parent, in ChildPlacement placement)
    {
        childRenderer?.RenderChild(parent, this, placement);
    }

    public void Fill(int x, int y, int width, int height, Rune rune, Style style)
    {
        var styleIndex = context.Styles.Intern(style);
        FillPreparedGlyph(x, y, width, height, PrepareRune(rune), styleIndex);
    }

    public void ClearRect(int x, int y, int width, int height, Style style)
    {
        var styleIndex = context.Styles.Intern(style);
        var visualX = x - OffsetX;
        var visualY = y - OffsetY;
        var startX = Math.Max(0, visualX);
        var startY = Math.Max(0, visualY);
        var endX = Math.Min(bounds.Width, visualX + width);
        var endY = Math.Min(bounds.Height, visualY + height);
        if (startX >= endX || startY >= endY) return;

        var absX = bounds.X + startX;
        var absY = bounds.Y + startY;
        var clippedWidth = endX - startX;
        var clippedHeight = endY - startY;

        buffer.ClearRect(absX, absY, clippedWidth, clippedHeight, styleIndex, frameNumber);
    }

    public void FillPreparedGlyph(int x,
                                  int y,
                                  int width,
                                  int height,
                                  PreparedGlyph glyph,
                                  int styleIndex)
    {
        if (width <= 0 || height <= 0 || glyph.Width <= 0) return;

        if (glyph.Width == 1)
        {
            var visualX = x - OffsetX;
            var visualY = y - OffsetY;
            var startX = Math.Max(0, visualX);
            var startY = Math.Max(0, visualY);
            var endX = Math.Min(bounds.Width, visualX + width);
            var endY = Math.Min(bounds.Height, visualY + height);
            if (startX < endX && startY < endY)
            {
                var absX = bounds.X + startX;
                var absY = bounds.Y + startY;
                var clippedWidth = endX - startX;
                var clippedHeight = endY - startY;

                buffer.FillRect(absX, absY, clippedWidth, clippedHeight,
                    new Cell(glyph.GlyphId, styleIndex, 1, 0), frameNumber);
            }

            return;
        }

        for (var dy = 0; dy < height; dy++)
        for (var dx = 0; dx < width; dx++)
            SetCell(x + dx, y + dy, glyph.GlyphId, styleIndex, glyph.Width);
    }

    public void PutGlyph(int x, int y, Rune rune, Style style)
    {
        var styleIndex = context.Styles.Intern(style);
        PutPreparedGlyph(x, y, PrepareRune(rune), styleIndex);
    }

    public void PutGlyph(int x, int y, string cluster, Style style)
    {
        var styleIndex = context.Styles.Intern(style);
        PutPreparedGlyph(x, y, PrepareCluster(cluster), styleIndex);
    }

    public void PutPreparedGlyph(int x, int y, PreparedGlyph glyph, int styleIndex)
    {
        SetCell(x, y, glyph.GlyphId, styleIndex, glyph.Width);
    }

    public PreparedGlyph PrepareRune(Rune rune)
    {
        return new(rune.Value, GetRuneWidth(rune));
    }

    public PreparedGlyph PrepareCluster(ReadOnlySpan<char> cluster)
    {
        if (cluster.Length == 0) return new(0, 0);

        if (cluster.Length == 1 && !char.IsSurrogate(cluster[0]))
            return new(cluster[0], GetWidth(cluster));

        if (cluster.Length == 2 && char.IsSurrogatePair(cluster[0], cluster[1]))
            return new(new Rune(cluster[0], cluster[1]).Value, GetWidth(cluster));

        return new(~context.Glyphs.Intern(new string(cluster)), GetWidth(cluster));
    }

    public PreparedGlyph PrepareCluster(string cluster)
    {
        if (cluster.Length == 0) return new(0, 0);

        if (cluster.Length == 1 && !char.IsSurrogate(cluster[0]))
            return new(cluster[0], GetWidth(cluster));

        if (cluster.Length == 2 && char.IsSurrogatePair(cluster[0], cluster[1]))
            return new(new Rune(cluster[0], cluster[1]).Value, GetWidth(cluster));

        return new(~context.Glyphs.Intern(cluster), GetWidth(cluster));
    }

    public void PutPreparedGlyph(int x, int y, string grapheme, int styleIndex, byte width)
    {
        PutPreparedGlyph(x, y, grapheme.AsSpan(), styleIndex, width);
    }

    public void PutPreparedGlyph(int x, int y, ReadOnlySpan<char> grapheme, int styleIndex, byte width)
    {
        int glyphId;
        if (grapheme.Length == 1 && !char.IsSurrogate(grapheme[0]))
        {
            glyphId = grapheme[0];
        }
        else if (grapheme.Length == 2 && char.IsSurrogatePair(grapheme[0], grapheme[1]))
        {
            glyphId = new Rune(grapheme[0], grapheme[1]).Value;
        }
        else
        {
            glyphId = ~context.Glyphs.Intern(new string(grapheme));
        }

        SetCell(x, y, glyphId, styleIndex, width);
    }

    public void DrawStringWithStyleIndex(int x, int y, string text, int styleIndex)
    {
        var drawer = new CanvasWrappedDrawer(this, x, y, styleIndex);
        WrappedTextLayout.Draw(text.AsSpan(),
                               bounds.Width + OffsetX,
                               ref drawer,
                               context.UiContext.WidthProvider);
    }

    public int DrawTokenLine(int x,
                             int y,
                             ReadOnlySpan<TextToken> tokens,
                             int tokenStart,
                             int tokenCount,
                             ReadOnlySpan<TextTokenizer.TextRun> runs,
                             ReadOnlySpan<int> styleIndices)
    {
        var nextX = x;
        var canvasWidth = bounds.Width + OffsetX;
        var tokenEnd = tokenStart + tokenCount;

        for (var i = tokenStart; i < tokenEnd; i++)
        {
            if (nextX >= canvasWidth) break;

            var token = tokens[i];
            if (token.Kind == TokenKind.NewLine) continue;

            var runBytes = runs[token.RunIndex].Utf8.Span;
            var tokenUtf8 = runBytes.Slice(token.ByteStart, token.ByteLength);
            var styleIndex = styleIndices[token.RunIndex];

            var remaining = canvasWidth - nextX;
            if (token.EstimatedWidth < remaining)
            {
                nextX = DrawTokenComfortable(nextX, y, tokenUtf8, styleIndex);
            }
            else
            {
                nextX = DrawUtf8WithStyleIndex(nextX, y, tokenUtf8, styleIndex);
            }
        }

        return nextX;
    }

    int DrawTokenComfortable(int x, int y, ReadOnlySpan<byte> utf8, int styleIndex)
    {
        var visualY = y - OffsetY;
        var canWriteRowUnchecked = visualY >= 0 && visualY < bounds.Height;
        var absY = bounds.Y + visualY;

        for (var i = 0; i < utf8.Length;)
        {
            var b = utf8[i];
            if (b >= 128)
                return DrawUtf8WithStyleIndex(x, y, utf8[i..], styleIndex);

            var ch = (char)b;
            if (ch == '\t')
            {
                x += 4;
                i++;
                continue;
            }

            if (char.IsControl(ch))
            {
                i++;
                continue;
            }

            var runStart = i;
            var runX = x;
            i++;

            while (i < utf8.Length)
            {
                b = utf8[i];
                if (b >= 128) break;

                ch = (char)b;
                if (ch == '\t' || char.IsControl(ch)) break;
                i++;
            }

            var runLength = i - runStart;
            if (runLength <= 0) continue;

            var visualX = runX - OffsetX;
            if (canWriteRowUnchecked && visualX >= 0 && visualX + runLength <= bounds.Width)
            {
                var absX = bounds.X + visualX;
                buffer.WriteAsciiRunUnchecked(absX,
                                              absY,
                                              utf8.Slice(runStart, runLength),
                                              styleIndex,
                                              frameNumber);
            }
            else
            {
                for (var j = 0; j < runLength; j++)
                    SetCell(runX + j, y, utf8[runStart + j], styleIndex, 1);
            }

            x += runLength;
        }

        return x;
    }

    public int DrawUtf8ClippedWithStyleIndex(int x,
                                              int y,
                                              ReadOnlySpan<byte> utf8,
                                              int styleIndex,
                                              int maxWidth)
    {
        if (utf8.Length == 0 || maxWidth <= 0) return x;

        var nextX = x;
        var budget = maxWidth;

        for (var i = 0; i < utf8.Length; i++)
        {
            if (budget <= 0) break;

            var b = utf8[i];
            if (b < 128)
            {
                var ch = (char)b;
                if (ch == '\t') { var step = Math.Min(4, budget); nextX += step; budget -= step; continue; }
                if (char.IsControl(ch)) continue;
                SetCell(nextX, y, ch, styleIndex, 1);
                nextX++;
                budget--;
            }
            else
            {
                return DrawUtf8ClippedNonAscii(nextX, y, utf8[i..], styleIndex, budget);
            }
        }

        return nextX;
    }

    int DrawUtf8ClippedNonAscii(int x, int y, ReadOnlySpan<byte> utf8, int styleIndex, int budget)
    {
        var charCount = Encoding.UTF8.GetCharCount(utf8);
        if (charCount <= 0) return x;

        Span<char> stackMem = stackalloc char[256];
        using var buf = StackBuffer<char>.Create(stackMem, charCount);
        var chars = buf.Span;
        var written = Encoding.UTF8.GetChars(utf8, chars);
        var nextX = x;
        foreach (var grapheme in chars[..written].EnumerateGraphemes())
        {
            var w = GetWidth(grapheme);
            if (w <= 0) continue;
            if (budget < w) break;
            DrawGrapheme(ref nextX, y, grapheme, styleIndex);
            budget -= w;
        }

        return nextX;
    }

    public int MeasureUtf8Width(ReadOnlySpan<byte> utf8)
    {
        if (utf8.Length == 0) return 0;

        var width = 0;

        for (var i = 0; i < utf8.Length; i++)
        {
            var b = utf8[i];
            if (b < 128)
            {
                var ch = (char)b;
                if (ch == '\t') { width += 4; continue; }
                if (char.IsControl(ch)) continue;
                width++;
            }
            else
            {
                return width + MeasureUtf8WidthNonAscii(utf8[i..]);
            }
        }

        return width;
    }

    int MeasureUtf8WidthNonAscii(ReadOnlySpan<byte> utf8)
    {
        var charCount = Encoding.UTF8.GetCharCount(utf8);
        if (charCount <= 0) return 0;

        Span<char> stackMem = stackalloc char[256];
        using var buf = StackBuffer<char>.Create(stackMem, charCount);
        var chars = buf.Span;
        var written = Encoding.UTF8.GetChars(utf8, chars);
        return MeasureGraphemes(chars[..written]);
    }

    public int DrawUtf8WithStyleIndex(int x, int y, ReadOnlySpan<byte> utf8, int styleIndex)
    {
        if (utf8.Length == 0) return x;

        var nextX = x;

        var allAscii = true;
        for (var i = 0; i < utf8.Length; i++)
        {
            if (utf8[i] < 128) continue;
            allAscii = false;
            break;
        }

        if (allAscii)
        {
            for (var i = 0; i < utf8.Length; i++)
            {
                var ch = (char)utf8[i];
                if (ch == '\t')
                {
                    nextX += 4;
                    continue;
                }

                if (char.IsControl(ch)) continue;
                SetCell(nextX, y, ch, styleIndex, 1);
                nextX++;
            }

            return nextX;
        }

        var charCount = Encoding.UTF8.GetCharCount(utf8);
        if (charCount <= 0) return nextX;

        Span<char> stackMem = stackalloc char[256];
        using var buf = StackBuffer<char>.Create(stackMem, charCount);
        var chars = buf.Span;
        var written = Encoding.UTF8.GetChars(utf8, chars);
        DrawGraphemes(ref nextX, y, chars[..written], styleIndex);

        return nextX;
    }

    struct CanvasWrappedDrawer(Canvas canvas, int originX, int originY, int styleIndex) : IWrappedTextDrawer
    {
        public void DrawGrapheme(ReadOnlySpan<char> grapheme, int x, int y)
        {
            canvas.DrawGraphemeAt(originX + x, originY + y, grapheme, styleIndex);
        }
    }

    void DrawGraphemes(ref int x, int y, ReadOnlySpan<char> text, int styleIndex)
    {
        foreach (var grapheme in text.EnumerateGraphemes())
            DrawGrapheme(ref x, y, grapheme, styleIndex);
    }

    public void DrawString(int x, int y, string text, Style style)
    {
        var styleIndex = context.Styles.Intern(style);
        DrawStringWithStyleIndex(x, y, text, styleIndex);
    }

    public void DrawString(int x, int y, string text, Style style, string? link)
    {
        var styleWithLink = link is null ? style : style with { Hyperlink = link };
        var styleIndex = context.Styles.Intern(styleWithLink);
        DrawStringWithStyleIndex(x, y, text, styleIndex);
    }

    void DrawGrapheme(ref int x, int y, ReadOnlySpan<char> grapheme, int styleIndex)
    {
        if (grapheme.Length == 1 && grapheme[0] == '\t')
        {
            x += 4;
            return;
        }

        if (grapheme.Length == 1 && (char.IsControl(grapheme[0]) || char.IsSurrogate(grapheme[0])))
            return;

        int glyphId;
        byte charWidth;

        if (grapheme.Length == 1 && !char.IsSurrogate(grapheme[0]))
        {
            glyphId = grapheme[0];
            charWidth = GetWidth(grapheme);
        }
        else if (grapheme.Length == 2 && char.IsSurrogatePair(grapheme[0], grapheme[1]))
        {
            var rune = new Rune(grapheme[0], grapheme[1]);
            glyphId = rune.Value;
            charWidth = GetWidth(grapheme);
        }
        else
        {
            glyphId = ~context.Glyphs.Intern(new(grapheme));
            charWidth = GetWidth(grapheme);
        }

        SetCell(x, y, glyphId, styleIndex, charWidth);
        x += charWidth;
    }

    void DrawGraphemeAt(int x, int y, ReadOnlySpan<char> grapheme, int styleIndex)
    {
        if (grapheme.Length == 1 && grapheme[0] == '\t') return;

        if (grapheme.Length == 1 && (char.IsControl(grapheme[0]) || char.IsSurrogate(grapheme[0])))
            return;

        int glyphId;
        byte charWidth;

        if (grapheme.Length == 1 && !char.IsSurrogate(grapheme[0]))
        {
            glyphId = grapheme[0];
            charWidth = GetWidth(grapheme);
        }
        else if (grapheme.Length == 2 && char.IsSurrogatePair(grapheme[0], grapheme[1]))
        {
            var rune = new Rune(grapheme[0], grapheme[1]);
            glyphId = rune.Value;
            charWidth = GetWidth(grapheme);
        }
        else
        {
            glyphId = ~context.Glyphs.Intern(new(grapheme));
            charWidth = GetWidth(grapheme);
        }

        SetCell(x, y, glyphId, styleIndex, charWidth);
    }

    void SetCell(int x, int y, int glyphId, int styleIndex, byte width)
    {
        var visualX = x - OffsetX;
        var visualY = y - OffsetY;

        if (visualX < 0 || visualX + width > bounds.Width || visualY < 0 ||
            visualY >= bounds.Height)
            return;

        var absX = bounds.X + visualX;
        var absY = bounds.Y + visualY;

        buffer.SetCellUnchecked(absX, absY, new Cell(glyphId, styleIndex, width, frameNumber));

        for (var i = 1; i < width; i++)
            buffer.SetCellUnchecked(absX + i, absY, new Cell(0, styleIndex, 0, frameNumber));
    }

    public Canvas Slice(Rect subBounds)
    {
        var physicalX = bounds.X + subBounds.X - OffsetX;
        var physicalY = bounds.Y + subBounds.Y - OffsetY;

        if (subBounds.Width < 0 || subBounds.Height < 0)
            throw new InvalidOperationException(
                $"Slice rectangle has negative size: {subBounds}.");

        var minX = bounds.X;
        var minY = bounds.Y;
        var maxX = bounds.X + bounds.Width;
        var maxY = bounds.Y + bounds.Height;
        var endX = physicalX + subBounds.Width;
        var endY = physicalY + subBounds.Height;

        if (physicalX < minX || physicalY < minY || endX > maxX || endY > maxY)
            throw new InvalidOperationException(
                $"Slice rectangle {subBounds} resolved to ({physicalX},{physicalY},{subBounds.Width},{subBounds.Height}) outside parent bounds ({bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}).");

        return new(buffer,
                   new(physicalX, physicalY, subBounds.Width, subBounds.Height),
                   context,
                   0,
                   0,
                   frameNumber,
                   childRenderer);
    }

    byte GetRuneWidth(Rune rune)
    {
        Span<char> runeBuffer = stackalloc char[2];
        var length = rune.EncodeToUtf16(runeBuffer);
        return context.UiContext.WidthProvider.GetWidth(runeBuffer[..length]);
    }

    byte GetWidth(ReadOnlySpan<char> grapheme)
    {
        if (grapheme.Length == 1 && grapheme[0] == '\t') return 4;
        if (grapheme.Length == 1 && (char.IsControl(grapheme[0]) || char.IsSurrogate(grapheme[0])))
            return 0;
        return context.UiContext.WidthProvider.GetWidth(grapheme);
    }

    int MeasureGraphemes(ReadOnlySpan<char> text)
    {
        var width = 0;
        foreach (var grapheme in text.EnumerateGraphemes()) width += GetWidth(grapheme);
        return width;
    }
}

internal interface ICanvasChildRenderer
{
    int RenderChildCallCount { get; }
    void RenderChild(IWidget parent, Canvas parentCanvas, in Canvas.ChildPlacement placement);
}
