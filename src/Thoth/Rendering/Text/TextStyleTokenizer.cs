using Thoth.Widgets;

namespace Thoth.Rendering.Text;

public sealed class TextStyleTokenizer
{
    readonly List<TextStyleSpan> _scratchSpans = [];

    public TextStyleDelta Tokenize(IReadOnlyList<TextStyleRun> runs)
    {
        var spans = new List<TextStyleSpan>(Math.Max(4, runs.Count));
        TokenizeInto(runs, spans);
        return new(spans, 0, 0);
    }

    public TextStyleDelta ApplyEdit(IReadOnlyList<TextStyleRun> runs,
                                    in TextEdit edit,
                                    IReadOnlyList<TextStyleSpan> currentSpans)
    {
        _scratchSpans.Clear();
        if (_scratchSpans.Capacity < Math.Max(currentSpans.Count, runs.Count))
            _scratchSpans.Capacity = Math.Max(currentSpans.Count, runs.Count);

        TokenizeInto(runs, _scratchSpans);

        var affectedStart = Math.Clamp(edit.AffectedTokenStart, 0, currentSpans.Count);
        var affectedEnd = edit.AffectedTokenEnd < affectedStart
            ? affectedStart - 1
            : Math.Clamp(edit.AffectedTokenEnd, affectedStart, Math.Max(affectedStart - 1, currentSpans.Count - 1));

        var prefix = 0;
        var maxPrefix = Math.Min(affectedStart, Math.Min(currentSpans.Count, _scratchSpans.Count));
        while (prefix < maxPrefix && currentSpans[prefix] == _scratchSpans[prefix])
            prefix++;

        var suffixOldStart = Math.Max(prefix, affectedEnd + 1);
        var maxSuffixByOld = Math.Max(0, currentSpans.Count - suffixOldStart);
        var maxSuffix = Math.Min(maxSuffixByOld, Math.Max(0, _scratchSpans.Count - prefix));

        var suffix = 0;
        while (suffix < maxSuffix)
        {
            var oldSpan = currentSpans[currentSpans.Count - 1 - suffix];
            var newSpan = _scratchSpans[_scratchSpans.Count - 1 - suffix];
            if (oldSpan != newSpan) break;
            suffix++;
        }

        var replaceStart = prefix;
        var replaceCount = Math.Max(0, currentSpans.Count - prefix - suffix);
        var insertCount = Math.Max(0, _scratchSpans.Count - prefix - suffix);

        if (insertCount == 0)
            return new(Array.Empty<TextStyleSpan>(), replaceStart, replaceCount);

        var patch = new TextStyleSpan[insertCount];
        for (var i = 0; i < insertCount; i++)
            patch[i] = _scratchSpans[prefix + i];

        return new(patch, replaceStart, replaceCount);
    }

    static void TokenizeInto(IReadOnlyList<TextStyleRun> runs, List<TextStyleSpan> spans)
    {
        spans.Clear();

        for (var runIndex = 0; runIndex < runs.Count; runIndex++)
        {
            var run = runs[runIndex];
            var byteLength = run.Utf8.Length;
            if (byteLength <= 0) continue;

            spans.Add(new(runIndex, 0, byteLength, run.StyleId, run.LinkId));
        }
    }
}

public readonly record struct TextStyleRun(ReadOnlyMemory<byte> Utf8,
                                           StyleId? StyleId,
                                           LinkId? LinkId = null);

