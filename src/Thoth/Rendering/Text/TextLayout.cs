using Thoth.Widgets;

namespace Thoth.Rendering.Text;

public sealed class TextLayout
{
    readonly List<TextToken> _tokens = [];
    readonly List<TextLine> _lines = [];

    public List<TextToken> Tokens => _tokens;
    public List<TextLine> Lines => _lines;

    public void Initialize(TokenDelta delta)
    {
        _tokens.Clear();
        if (_tokens.Capacity < delta.Tokens.Count)
            _tokens.Capacity = delta.Tokens.Count;
        for (var i = 0; i < delta.Tokens.Count; i++)
            _tokens.Add(delta.Tokens[i]);
    }

    public void ApplyTokenDelta(TokenDelta delta)
    {
        var start = Math.Clamp(delta.ReplaceTokenStart, 0, _tokens.Count);
        var count = Math.Clamp(delta.ReplaceTokenCount, 0, _tokens.Count - start);

        if (count > 0)
            _tokens.RemoveRange(start, count);

        for (var i = 0; i < delta.Tokens.Count; i++)
            _tokens.Insert(start + i, delta.Tokens[i]);
    }

    public void Reflow(int maxWidth, TextOverflow overflow, int maxLines = int.MaxValue)
    {
        _lines.Clear();

        maxWidth = Math.Max(1, maxWidth);
        maxLines = overflow == TextOverflow.Clip
            ? 1
            : Math.Max(1, maxLines);

        var lineStart = 0;
        var lineWidth = 0;

        for (var i = 0; i < _tokens.Count; i++)
        {
            var token = _tokens[i];
            if (token.Kind == TokenKind.NewLine)
            {
                AddLine(lineStart, i - lineStart, lineWidth, maxLines);
                if (_lines.Count >= maxLines) return;
                lineStart = i + 1;
                lineWidth = 0;
                continue;
            }

            if (lineWidth + token.EstimatedWidth > maxWidth && i > lineStart)
            {
                AddLine(lineStart, i - lineStart, lineWidth, maxLines);
                if (_lines.Count >= maxLines) return;
                lineStart = i;
                lineWidth = 0;
            }

            lineWidth += token.EstimatedWidth;
        }

        if (lineStart < _tokens.Count || _lines.Count == 0)
            AddLine(lineStart, _tokens.Count - lineStart, lineWidth, maxLines);
    }

    public IEnumerable<LineTokenPlacement> EnumerateLine(int lineIndex)
    {
        if (lineIndex < 0 || lineIndex >= _lines.Count)
            yield break;

        var line = _lines[lineIndex];
        var x = 0;

        for (var i = line.TokenStart; i < line.TokenStart + line.TokenCount; i++)
        {
            var token = _tokens[i];
            yield return new(i, x, token);
            x += token.EstimatedWidth;
        }
    }

    void AddLine(int tokenStart, int tokenCount, int estimatedWidth, int maxLines)
    {
        if (_lines.Count >= maxLines) return;
        _lines.Add(new(tokenStart, Math.Max(0, tokenCount), Math.Max(0, estimatedWidth)));
    }
}

public readonly record struct TextLine(int TokenStart, int TokenCount, int EstimatedWidth);

public readonly record struct LineTokenPlacement(int TokenIndex, int X, TextToken Token);
