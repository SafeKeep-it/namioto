using System.Buffers;
using System.Text;

namespace Thoth.Rendering.Text;

public ref struct GraphemeEnumerator
{
    readonly ReadOnlySpan<char> _text;
    int _index;
    int _currentStart;
    int _currentLength;

    public GraphemeEnumerator(ReadOnlySpan<char> text)
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

        var c = _text[_index];

        if (c < 128 && !char.IsControl(c))
        {
            _index++;
            // Check for following combining marks
            while (_index < _text.Length && IsGraphemeExtender(_text, _index, out var extLen))
            {
                _index += extLen;
            }

            _currentLength = _index - _currentStart;
            return true;
        }

        if (Rune.DecodeFromUtf16(_text[_index..], out var rune, out var _) !=
            OperationStatus.Done)
        {
            _currentLength = 1;
            _index++;
            return true;
        }

        var len = rune.Utf16SequenceLength;
        _index += len;

        while (_index < _text.Length && IsGraphemeExtender(_text, _index, out var extLen))
        {
            _index += extLen;
        }

        _currentLength = _index - _currentStart;
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

        if (value == 0x200D) return true;
        if (value >= 0xFE00 && value <= 0xFE0F) return true;
        if (value >= 0xE0100 && value <= 0xE01EF) return true;
        if (value >= 0x0300 && value <= 0x036F) return true;
        if (value >= 0x1AB0 && value <= 0x1AFF) return true;
        if (value >= 0x20D0 && value <= 0x20FF) return true;
        if (value >= 0xFE20 && value <= 0xFE2F) return true;
        if (value >= 0x1F3FB && value <= 0x1F3FF) return true;

        return false;
    }

    public GraphemeEnumerator GetEnumerator() => this;
}