using System.Buffers;
using System.Text;

namespace Thoth.Rendering.Text;

/// <summary>
/// Zero-allocation word enumerator that splits text at spaces, hyphens, and tabs.
/// Words include trailing delimiters (e.g., "hello " or "well-").
/// </summary>
public ref struct WordEnumerator
{
    readonly ReadOnlySpan<char> _text;
    int _index;
    int _currentStart;
    int _currentLength;

    public WordEnumerator(ReadOnlySpan<char> text)
    {
        _text = text;
        _index = 0;
        _currentStart = 0;
        _currentLength = 0;
    }

    public ReadOnlySpan<char> Current => _text.Slice(_currentStart, _currentLength);

    public bool MoveNext()
    {
        if (_index >= _text.Length) return false;

        _currentStart = _index;

        while (_index < _text.Length)
        {
            var graphemeStart = _index;

            // Get next grapheme cluster
            if (!TryGetNextGrapheme(out var graphemeLength))
            {
                _index++;
                continue;
            }

            _index += graphemeLength;
            var grapheme = _text.Slice(graphemeStart, graphemeLength);

            // Check if this is a word boundary (space, hyphen, tab)
            if (grapheme.Length == 1)
            {
                var c = grapheme[0];
                if (c == ' ' || c == '-' || c == '\t')
                {
                    _currentLength = _index - _currentStart;
                    return true;
                }
            }
        }

        _currentLength = _index - _currentStart;
        return _currentLength > 0;
    }

    bool TryGetNextGrapheme(out int length)
    {
        if (_index >= _text.Length)
        {
            length = 0;
            return false;
        }

        var c = _text[_index];

        // Fast path: ASCII non-control
        if (c < 128 && !char.IsControl(c))
        {
            length = 1;
            return true;
        }

        // Decode rune
        if (Rune.DecodeFromUtf16(_text[_index..], out var rune, out var _) !=
            OperationStatus.Done)
        {
            length = 1;
            return true;
        }

        var runeLen = rune.Utf16SequenceLength;
        var end = _index + runeLen;

        // Check for grapheme extenders
        while (end < _text.Length && IsGraphemeExtender(_text, end, out var extLen))
        {
            end += extLen;
        }

        length = end - _index;
        return true;
    }

    static bool IsGraphemeExtender(ReadOnlySpan<char> text, int index, out int length)
    {
        if (Rune.DecodeFromUtf16(text[index..], out var rune, out var _) !=
            OperationStatus.Done)
        {
            length = 0;
            return false;
        }

        length = rune.Utf16SequenceLength;
        var value = rune.Value;

        // Zero-width joiner
        if (value == 0x200D) return true;
        // Variation selectors
        if (value >= 0xFE00 && value <= 0xFE0F) return true;
        // Variation selectors supplement
        if (value >= 0xE0100 && value <= 0xE01EF) return true;
        // Combining diacritical marks
        if (value >= 0x0300 && value <= 0x036F) return true;
        // Combining diacritical marks extended
        if (value >= 0x1AB0 && value <= 0x1AFF) return true;
        // Combining diacritical marks for symbols
        if (value >= 0x20D0 && value <= 0x20FF) return true;
        // Combining half marks
        if (value >= 0xFE20 && value <= 0xFE2F) return true;
        // Emoji skin tone modifiers
        if (value >= 0x1F3FB && value <= 0x1F3FF) return true;

        return false;
    }

    public WordEnumerator GetEnumerator() => this;
}