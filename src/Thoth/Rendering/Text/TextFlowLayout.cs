using Thoth.Widgets;

namespace Thoth.Rendering.Text;

public static class TextFlowLayout
{
    static readonly IWidthProvider DefaultWidthProvider = new UnicodeWidthProvider();

    public static FlowResult Build(IReadOnlyList<TextRun> runs,
                                   int maxWidth,
                                   TextOverflow overflow,
                                   IWidthProvider? widthProvider = null,
                                   int maxLines = int.MaxValue)
    {
        maxWidth = Math.Max(1, maxWidth);
        maxLines = Math.Max(1, maxLines);
        widthProvider ??= DefaultWidthProvider;

        var lines = new List<FlowLine>(EstimateLineCapacity(runs, maxLines));
        if (runs.Count == 0) return new(lines, 0, 0);

        if (overflow == TextOverflow.Wrap)
            LayoutWrap(lines, runs, maxWidth, widthProvider, maxLines);
        else if (overflow == TextOverflow.Marquee)
            LayoutSingleLine(lines, runs, int.MaxValue, useEllipsis: false, widthProvider, maxLines);
        else
            LayoutSingleLine(lines, runs, maxWidth, overflow == TextOverflow.Ellipsis, widthProvider, maxLines);

        var width = 0;
        for (var i = 0; i < lines.Count; i++)
            width = Math.Max(width, lines[i].Width);

        return new(lines, width, lines.Count);
    }

    static void LayoutWrap(List<FlowLine> lines,
                           IReadOnlyList<TextRun> runs,
                           int maxWidth,
                           IWidthProvider widthProvider,
                           int maxLines)
    {
        var currentLine = CreateLine(maxWidth);
        var currentX = 0;

        for (var runIndex = 0; runIndex < runs.Count; runIndex++)
        {
            var run = runs[runIndex];
            var textSpan = run.Text.AsSpan();
            var lineStart = 0;
            var firstLine = true;

            while (lineStart <= textSpan.Length)
            {
                var lineEnd = textSpan[lineStart..].IndexOf('\n');
                var line = lineEnd >= 0
                    ? textSpan.Slice(lineStart, lineEnd)
                    : textSpan[lineStart..];

                if (line.Length > 0 && line[^1] == '\r') line = line[..^1];

                if (!firstLine)
                {
                    if (!TryAddLine(lines, currentLine, maxLines)) return;
                    currentLine = CreateLine(maxWidth);
                    currentX = 0;
                }

                firstLine = false;

                if (line.Length > 0)
                {
                    foreach (var word in line.EnumerateWords())
                    {
                        AppendWord(lines,
                                   ref currentLine,
                                   ref currentX,
                                   run.StyleId,
                                   run.LinkId,
                                   word,
                                   maxWidth,
                                   widthProvider,
                                   maxLines);
                        if (lines.Count >= maxLines) return;
                    }
                }

                if (lineEnd < 0) break;
                lineStart += lineEnd + 1;
            }
        }

        if (currentLine.Segments.Count > 0 || lines.Count == 0)
            _ = TryAddLine(lines, currentLine, maxLines);
    }

    static void LayoutSingleLine(List<FlowLine> lines,
                                 IReadOnlyList<TextRun> runs,
                                 int maxWidth,
                                 bool useEllipsis,
                                 IWidthProvider widthProvider,
                                 int maxLines)
    {
        var currentLine = CreateLine(maxWidth);
        var currentWidth = 0;
        var truncated = false;
        StyleId? truncatedStyle = null;
        LinkId? truncatedLink = null;

        for (var runIndex = 0; runIndex < runs.Count; runIndex++)
        {
            var run = runs[runIndex];
            var textSpan = run.Text.AsSpan();
            var lineStart = 0;

            while (lineStart <= textSpan.Length)
            {
                var lineEnd = textSpan[lineStart..].IndexOf('\n');
                var line = lineEnd >= 0
                    ? textSpan.Slice(lineStart, lineEnd)
                    : textSpan[lineStart..];

                if (line.Length > 0 && line[^1] == '\r') line = line[..^1];

                AppendLineNoWrap(line,
                                 run.StyleId,
                                 run.LinkId,
                                 maxWidth,
                                 widthProvider,
                                 currentLine,
                                 ref currentWidth,
                                 ref truncated,
                                 ref truncatedStyle,
                                 ref truncatedLink);

                if (lineEnd < 0) break;

                if (useEllipsis && truncated)
                    AppendEllipsis(currentLine,
                                   ref currentWidth,
                                   maxWidth,
                                   truncatedStyle,
                                   truncatedLink,
                                   widthProvider);

                if (!TryAddLine(lines, currentLine, maxLines)) return;

                currentLine = CreateLine(maxWidth);
                currentWidth = 0;
                truncated = false;
                truncatedStyle = null;
                truncatedLink = null;
                lineStart += lineEnd + 1;
            }
        }

        if (useEllipsis && truncated)
            AppendEllipsis(currentLine,
                           ref currentWidth,
                           maxWidth,
                           truncatedStyle,
                           truncatedLink,
                           widthProvider);

        if (currentLine.Segments.Count > 0 || lines.Count == 0)
            _ = TryAddLine(lines, currentLine, maxLines);
    }

    static void AppendLineNoWrap(ReadOnlySpan<char> line,
                                 StyleId? styleId,
                                 LinkId? linkId,
                                 int maxWidth,
                                 IWidthProvider widthProvider,
                                 FlowLine lineState,
                                 ref int currentWidth,
                                 ref bool truncated,
                                 ref StyleId? truncatedStyle,
                                 ref LinkId? truncatedLink)
    {
        if (line.Length == 0 || truncated) return;

        foreach (var grapheme in line.EnumerateGraphemes())
        {
            var elementWidth = MeasureGraphemeWidth(grapheme, widthProvider);
            if (currentWidth + elementWidth > maxWidth)
            {
                truncated = true;
                truncatedStyle = styleId;
                truncatedLink = linkId;
                return;
            }

            lineState.AddGrapheme(grapheme, (byte)elementWidth, styleId, linkId);
            currentWidth += elementWidth;
        }
    }

    static void AppendEllipsis(FlowLine line,
                               ref int currentWidth,
                               int maxWidth,
                               StyleId? truncatedStyle,
                               LinkId? truncatedLink,
                               IWidthProvider widthProvider)
    {
        if (maxWidth <= 0) return;

        const string ellipsis = "…";
        var ellipsisWidth = widthProvider.GetWidth(ellipsis.AsSpan());
        if (ellipsisWidth > maxWidth) return;

        while (currentWidth + ellipsisWidth > maxWidth)
        {
            if (!line.TryPopLastCell(out var removed)) break;
            currentWidth -= removed.Width;
        }

        if (currentWidth + ellipsisWidth > maxWidth) return;

        var styleId = line.LastStyleId ?? truncatedStyle;
        var linkId = line.LastLinkId ?? truncatedLink;
        line.AddGrapheme(ellipsis.AsSpan(), ellipsisWidth, styleId, linkId);
        currentWidth += ellipsisWidth;
    }

    static void AppendWord(List<FlowLine> lines,
                           ref FlowLine currentLine,
                           ref int currentX,
                           StyleId? styleId,
                           LinkId? linkId,
                           ReadOnlySpan<char> word,
                           int maxWidth,
                           IWidthProvider widthProvider,
                           int maxLines)
    {
        var wordWidth = MeasureWordWidth(word, widthProvider);

        if (currentX + wordWidth > maxWidth && currentX > 0)
        {
            if (!TryAddLine(lines, currentLine, maxLines)) return;
            currentLine = new();
            currentX = 0;
        }

        if (wordWidth > maxWidth)
        {
            AppendLongWord(lines,
                           ref currentLine,
                           ref currentX,
                           styleId,
                           linkId,
                           word,
                           maxWidth,
                           widthProvider,
                           maxLines);
            return;
        }

        foreach (var grapheme in word.EnumerateGraphemes())
        {
            var elementWidth = MeasureGraphemeWidth(grapheme, widthProvider);
            currentLine.AddGrapheme(grapheme, (byte)elementWidth, styleId, linkId);
            currentX += elementWidth;
        }
    }

    static void AppendLongWord(List<FlowLine> lines,
                               ref FlowLine currentLine,
                               ref int currentX,
                               StyleId? styleId,
                               LinkId? linkId,
                               ReadOnlySpan<char> word,
                               int maxWidth,
                               IWidthProvider widthProvider,
                               int maxLines)
    {
        foreach (var grapheme in word.EnumerateGraphemes())
        {
            var elementWidth = MeasureGraphemeWidth(grapheme, widthProvider);
            if (currentX + elementWidth > maxWidth && currentX > 0)
            {
                if (!TryAddLine(lines, currentLine, maxLines)) return;
                currentLine = new();
                currentX = 0;
            }

            currentLine.AddGrapheme(grapheme, (byte)elementWidth, styleId, linkId);
            currentX += elementWidth;
        }
    }

    static int MeasureWordWidth(ReadOnlySpan<char> word, IWidthProvider widthProvider)
    {
        var width = 0;
        foreach (var grapheme in word.EnumerateGraphemes())
            width += MeasureGraphemeWidth(grapheme, widthProvider);
        return width;
    }

    static int MeasureGraphemeWidth(ReadOnlySpan<char> grapheme, IWidthProvider widthProvider)
    {
        if (grapheme.Length == 1 && grapheme[0] == '\t') return 4;
        return widthProvider.GetWidth(grapheme);
    }

    static bool TryAddLine(List<FlowLine> lines, FlowLine line, int maxLines)
    {
        if (lines.Count >= maxLines) return false;
        lines.Add(line);
        return true;
    }

    static int EstimateLineCapacity(IReadOnlyList<TextRun> runs, int maxLines)
    {
        if (maxLines <= 0) return 1;
        if (maxLines == int.MaxValue) return Math.Max(4, runs.Count * 2);
        return Math.Max(1, Math.Min(maxLines, runs.Count * 2));
    }

    static FlowLine CreateLine(int maxWidth)
    {
        var expectedCells = Math.Clamp(maxWidth, 8, 256);
        return new(expectedCells);
    }
}

public readonly record struct FlowResult(IReadOnlyList<FlowLine> Lines, int Width, int Height);

public sealed class FlowLine(int expectedCellCapacity = 32)
{
    static readonly GraphemePool GraphemePool = new();
    readonly int _segmentCellCapacity = Math.Max(4, expectedCellCapacity / 4);
    public List<FlowSegment> Segments { get; } = new(Math.Max(4, expectedCellCapacity / 8));
    public int Width { get; private set; }

    public StyleId? LastStyleId => Segments.Count > 0 ? Segments[^1].StyleId : null;
    public LinkId? LastLinkId => Segments.Count > 0 ? Segments[^1].LinkId : null;

    public void AddGrapheme(ReadOnlySpan<char> grapheme, byte width, StyleId? styleId, LinkId? linkId)
    {
        if (width <= 0) return;
        var text = GraphemePool.Intern(grapheme);

        if (Segments.Count > 0)
        {
            var last = Segments[^1];
            if (last.StyleId == styleId && last.LinkId == linkId)
            {
                last.AddCell(new(text, width));
                Width += width;
                return;
            }
        }

        var segment = new FlowSegment(styleId, linkId, _segmentCellCapacity);
        segment.AddCell(new(text, width));
        Segments.Add(segment);
        Width += width;
    }

    public bool TryPopLastCell(out FlowCell removed)
    {
        removed = default;
        if (Segments.Count == 0) return false;

        var segment = Segments[^1];
        if (segment.Cells.Count == 0)
        {
            Segments.RemoveAt(Segments.Count - 1);
            return false;
        }

        removed = segment.Cells[^1];
        segment.Cells.RemoveAt(segment.Cells.Count - 1);
        segment.Width -= removed.Width;
        Width -= removed.Width;

        if (segment.Cells.Count == 0)
            Segments.RemoveAt(Segments.Count - 1);

        return true;
    }
}

public sealed class FlowSegment(StyleId? styleId, LinkId? linkId, List<FlowCell> cells)
{
    public StyleId? StyleId { get; } = styleId;
    public LinkId? LinkId { get; } = linkId;
    public List<FlowCell> Cells { get; } = cells;
    public int Width { get; set; }

    public FlowSegment(StyleId? styleId, LinkId? linkId, int initialCellCapacity)
        : this(styleId, linkId, new List<FlowCell>(Math.Max(1, initialCellCapacity)))
    {
    }

    public void AddCell(FlowCell cell)
    {
        Cells.Add(cell);
        Width += cell.Width;
    }
}

public readonly record struct FlowCell(string Text, byte Width);
