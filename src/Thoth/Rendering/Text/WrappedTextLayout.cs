namespace Thoth.Rendering.Text;

public interface IWrappedTextDrawer
{
    void DrawGrapheme(ReadOnlySpan<char> grapheme, int x, int y);
}

public static class WrappedTextLayout
{
    public static int MoveCaretToPreviousWord(ReadOnlySpan<char> text, int caretIndex)
    {
        var index = Math.Clamp(caretIndex, 0, text.Length);

        while (index > 0)
        {
            var previous = previous_grapheme_start(text, index);
            if (!is_whitespace_grapheme(text, previous)) break;
            index = previous;
        }

        while (index > 0)
        {
            var previous = previous_grapheme_start(text, index);
            if (is_whitespace_grapheme(text, previous)) break;
            index = previous;
        }

        return index;
    }

    public static int MoveCaretToNextWord(ReadOnlySpan<char> text, int caretIndex)
    {
        var index = Math.Clamp(caretIndex, 0, text.Length);

        while (index < text.Length && is_whitespace_grapheme(text, index))
            index = next_grapheme_start(text, index);

        while (index < text.Length && !is_whitespace_grapheme(text, index))
            index = next_grapheme_start(text, index);

        return index;
    }

    public static int MeasureHeight(ReadOnlySpan<char> text,
                                    int maxWidth,
                                    IWidthProvider? widthProvider = null)
    {
        maxWidth = Math.Max(1, maxWidth);
        var lineStart = 0;
        var height = 0;

        while (lineStart <= text.Length)
        {
            var rel = text[lineStart..].IndexOf('\n');
            var rawLine = rel >= 0 ? text.Slice(lineStart, rel) : text[lineStart..];
            var visibleLine = rawLine;
            if (!visibleLine.IsEmpty && visibleLine[^1] == '\r') visibleLine = visibleLine[..^1];

            var currentX = 0;
            var localIndex = 0;

            foreach (var word in visibleLine.EnumerateWords())
            {
                var wordWidth = measure_word(word, widthProvider);

                if (currentX + wordWidth > maxWidth && currentX > 0)
                {
                    height++;
                    currentX = 0;
                }

                if (wordWidth > maxWidth)
                {
                    foreach (var grapheme in word.EnumerateGraphemes())
                    {
                        var width = grapheme_width(grapheme, widthProvider);
                        if (currentX + width > maxWidth && currentX > 0)
                        {
                            height++;
                            currentX = 0;
                        }

                        currentX += width;
                        localIndex += grapheme.Length;
                    }
                }
                else
                {
                    currentX += wordWidth;
                    localIndex += word.Length;
                }
            }

            height++;

            if (rel < 0) break;
            lineStart += rel + 1;
        }

        return Math.Max(1, height);
    }

    public static (int x, int y) GetVisualPosition(ReadOnlySpan<char> text,
                                                    int caretIndex,
                                                    int maxWidth,
                                                    IWidthProvider? widthProvider = null)
    {
        maxWidth = Math.Max(1, maxWidth);
        var lineStart = 0;
        var absoluteIndex = 0;
        var y = 0;

        while (lineStart <= text.Length)
        {
            var rel = text[lineStart..].IndexOf('\n');
            var rawLine = rel >= 0 ? text.Slice(lineStart, rel) : text[lineStart..];
            var visibleLine = rawLine;
            if (!visibleLine.IsEmpty && visibleLine[^1] == '\r') visibleLine = visibleLine[..^1];

            var currentX = 0;
            var localIndex = 0;

            foreach (var word in visibleLine.EnumerateWords())
            {
                var wordWidth = measure_word(word, widthProvider);

                if (currentX + wordWidth > maxWidth && currentX > 0)
                {
                    y++;
                    currentX = 0;
                }

                foreach (var grapheme in word.EnumerateGraphemes())
                {
                    var absolute = absoluteIndex + localIndex;
                    if (absolute == caretIndex) return (currentX, y);

                    var width = grapheme_width(grapheme, widthProvider);
                    if (currentX + width > maxWidth && currentX > 0)
                    {
                        y++;
                        currentX = 0;
                    }

                    currentX += width;
                    localIndex += grapheme.Length;
                }
            }

            if (absoluteIndex + localIndex == caretIndex) return (currentX, y);

            y++;
            var suffix = rawLine.Length - visibleLine.Length;
            absoluteIndex += visibleLine.Length + suffix;

            if (rel < 0) break;
            absoluteIndex++;
            lineStart += rel + 1;
        }

        return (0, y);
    }

    public static int GetCaretIndexFromPoint(ReadOnlySpan<char> text,
                                             int x,
                                             int y,
                                             int maxWidth,
                                             IWidthProvider? widthProvider = null)
    {
        maxWidth = Math.Max(1, maxWidth);
        var lineStart = 0;
        var absoluteIndex = 0;
        var currentY = 0;

        while (lineStart <= text.Length)
        {
            var rel = text[lineStart..].IndexOf('\n');
            var rawLine = rel >= 0 ? text.Slice(lineStart, rel) : text[lineStart..];
            var visibleLine = rawLine;
            if (!visibleLine.IsEmpty && visibleLine[^1] == '\r') visibleLine = visibleLine[..^1];

            var boundaries = new List<(int X, int Index)> { (0, absoluteIndex) };
            var cx = 0;
            var localIndex = 0;

            foreach (var word in visibleLine.EnumerateWords())
            {
                var wordWidth = measure_word(word, widthProvider);
                if (cx + wordWidth > maxWidth && cx > 0)
                {
                    if (currentY == y) return find_nearest_boundary(boundaries, x);
                    currentY++;
                    cx = 0;
                    boundaries.Clear();
                    boundaries.Add((0, absoluteIndex + localIndex));
                }

                foreach (var grapheme in word.EnumerateGraphemes())
                {
                    var width = grapheme_width(grapheme, widthProvider);
                    if (cx + width > maxWidth && cx > 0)
                    {
                        if (currentY == y) return find_nearest_boundary(boundaries, x);
                        currentY++;
                        cx = 0;
                        boundaries.Clear();
                        boundaries.Add((0, absoluteIndex + localIndex));
                    }

                    cx += width;
                    localIndex += grapheme.Length;
                    boundaries.Add((cx, absoluteIndex + localIndex));
                }
            }

            if (currentY == y) return find_nearest_boundary(boundaries, x);

            currentY++;
            var suffix = rawLine.Length - visibleLine.Length;
            absoluteIndex += visibleLine.Length + suffix;

            if (rel < 0) break;
            absoluteIndex++;
            lineStart += rel + 1;
        }

        return absoluteIndex;
    }

    public static void Draw<TDrawer>(ReadOnlySpan<char> text,
                                     int maxWidth,
                                     ref TDrawer drawer,
                                     IWidthProvider? widthProvider = null)
        where TDrawer : struct, IWrappedTextDrawer
    {
        maxWidth = Math.Max(1, maxWidth);
        var lineStart = 0;
        var y = 0;

        while (lineStart <= text.Length)
        {
            var rel = text[lineStart..].IndexOf('\n');
            var rawLine = rel >= 0 ? text.Slice(lineStart, rel) : text[lineStart..];
            var visibleLine = rawLine;
            if (!visibleLine.IsEmpty && visibleLine[^1] == '\r') visibleLine = visibleLine[..^1];

            var cx = 0;

            foreach (var word in visibleLine.EnumerateWords())
            {
                var wordWidth = measure_word(word, widthProvider);
                if (cx + wordWidth > maxWidth && cx > 0)
                {
                    y++;
                    cx = 0;
                }

                foreach (var grapheme in word.EnumerateGraphemes())
                {
                    var width = grapheme_width(grapheme, widthProvider);
                    if (cx + width > maxWidth && cx > 0)
                    {
                        y++;
                        cx = 0;
                    }

                    drawer.DrawGrapheme(grapheme, cx, y);
                    cx += width;
                }
            }

            y++;

            if (rel < 0) break;
            lineStart += rel + 1;
        }
    }

    static int find_nearest_boundary(List<(int X, int Index)> boundaries, int x)
    {
        var nearest = boundaries[0];
        var nearestDistance = Math.Abs(x - nearest.X);
        for (var i = 1; i < boundaries.Count; i++)
        {
            var distance = Math.Abs(x - boundaries[i].X);
            if (distance >= nearestDistance) continue;
            nearest = boundaries[i];
            nearestDistance = distance;
        }

        return nearest.Index;
    }

    static int measure_word(ReadOnlySpan<char> word, IWidthProvider? widthProvider)
    {
        var width = 0;
        foreach (var grapheme in word.EnumerateGraphemes())
            width += grapheme_width(grapheme, widthProvider);
        return width;
    }

    static int grapheme_width(ReadOnlySpan<char> grapheme, IWidthProvider? widthProvider)
    {
        if (grapheme.Length == 1)
        {
            if (grapheme[0] == '\t') return 4;
            if (char.IsControl(grapheme[0]) || char.IsSurrogate(grapheme[0])) return 0;
        }

        if (widthProvider is not null) return widthProvider.GetWidth(grapheme);
        return TextMetrics.GetGraphemeWidth(grapheme);
    }

    static bool is_whitespace_grapheme(ReadOnlySpan<char> text, int start)
    {
        if (start < 0 || start >= text.Length) return false;
        foreach (var grapheme in text[start..].EnumerateGraphemes())
            return grapheme.Length == 1 && char.IsWhiteSpace(grapheme[0]);
        return false;
    }

    static int next_grapheme_start(ReadOnlySpan<char> text, int start)
    {
        if (start >= text.Length) return text.Length;
        foreach (var grapheme in text[start..].EnumerateGraphemes())
            return start + grapheme.Length;
        return text.Length;
    }

    static int previous_grapheme_start(ReadOnlySpan<char> text, int index)
    {
        if (index <= 0) return 0;

        var current = 0;
        var previous = 0;
        foreach (var grapheme in text.EnumerateGraphemes())
        {
            if (current >= index) break;
            previous = current;
            current += grapheme.Length;
        }

        return previous;
    }
}
