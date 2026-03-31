using System.Buffers;
using System.Text;

namespace Thoth.Rendering.Text;

public static class TextMetrics
{
    public static int GetRuneWidth(Rune rune)
    {
        var value = rune.Value;

        if (value < 32 || (value >= 0x7F && value <= 0x9F)) return 0;
        if (value == 0x00AD) return 0; // Soft hyphen

        if (value >= 0x0300 && value <= 0x036F) return 0;
        if (value >= 0x1AB0 && value <= 0x1AFF) return 0;
        if (value >= 0x20D0 && value <= 0x20FF) return 0;
        if (value >= 0xFE20 && value <= 0xFE2F) return 0;
        if (value >= 0xFE00 && value <= 0xFE0F) return 0;
        if (value >= 0xE0100 && value <= 0xE01EF) return 0;
        if (value == 0x200B || value == 0x200C || value == 0x200D || value == 0xFEFF) return 0;

        if (value >= 0x1F300) return 2;
        if ((value >= 0x1100 && value <= 0x115F) || value == 0x2329 || value == 0x232A ||
            (value >= 0x2E80 && value <= 0xA4CF && value != 0x303F) ||
            (value >= 0xAC00 && value <= 0xD7A3) || (value >= 0xF900 && value <= 0xFAFF) ||
            (value >= 0xFE10 && value <= 0xFE19) || (value >= 0xFE30 && value <= 0xFE6F) ||
            (value >= 0xFF00 && value <= 0xFF60) || (value >= 0xFFE0 && value <= 0xFFE6) ||
            (value >= 0x20000 && value <= 0x2FFFD) || (value >= 0x30000 && value <= 0x3FFFD))
            return 2;

        return 1;
    }

    /// <summary>
    ///     Measures the display width of a grapheme cluster.
    ///     Handles tabs (width 4), control characters (width 0), and Unicode.
    /// </summary>
    public static int GetGraphemeWidth(ReadOnlySpan<char> grapheme)
    {
        if (grapheme.Length == 1 && grapheme[0] == '\t') return 4;
        if (Rune.DecodeFromUtf16(grapheme, out var rune, out var _) == OperationStatus.Done)
            return GetRuneWidth(rune);
        return 1;
    }

    /// <summary>
    ///     Measures the total display width of text by summing grapheme widths.
    /// </summary>
    public static int MeasureGraphemes(ReadOnlySpan<char> text)
    {
        var width = 0;
        foreach (var grapheme in text.EnumerateGraphemes()) width += GetGraphemeWidth(grapheme);
        return width;
    }
}