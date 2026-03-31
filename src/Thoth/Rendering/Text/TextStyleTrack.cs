using Thoth.Widgets;

namespace Thoth.Rendering.Text;

public sealed class TextStyleTrack
{
    readonly List<TextStyleSpan> _spans = [];

    public IReadOnlyList<TextStyleSpan> Spans => _spans;

    public void Initialize(IReadOnlyList<TextStyleSpan> spans)
    {
        _spans.Clear();
        for (var i = 0; i < spans.Count; i++)
            _spans.Add(spans[i]);
    }

    public void ApplyDelta(TextStyleDelta delta)
    {
        var start = Math.Clamp(delta.ReplaceStart, 0, _spans.Count);
        var count = Math.Clamp(delta.ReplaceCount, 0, _spans.Count - start);

        if (count > 0)
            _spans.RemoveRange(start, count);

        for (var i = 0; i < delta.Spans.Count; i++)
            _spans.Insert(start + i, delta.Spans[i]);
    }

    public IEnumerable<TextStyleSpan> EnumerateForToken(TextToken token)
    {
        var tokenStart = token.ByteStart;
        var tokenEnd = token.ByteStart + token.ByteLength;

        for (var i = 0; i < _spans.Count; i++)
        {
            var span = _spans[i];
            if (span.RunIndex != token.RunIndex) continue;

            var spanStart = span.ByteStart;
            var spanEnd = span.ByteStart + span.ByteLength;
            if (spanEnd <= tokenStart || spanStart >= tokenEnd) continue;

            yield return span;
        }
    }
}

public readonly record struct TextStyleSpan(int RunIndex,
                                            int ByteStart,
                                            int ByteLength,
                                            StyleId? StyleId,
                                            LinkId? LinkId);

public readonly record struct TextStyleDelta(IReadOnlyList<TextStyleSpan> Spans,
                                             int ReplaceStart,
                                             int ReplaceCount);
