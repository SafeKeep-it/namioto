using System.Text;

namespace Thoth.Rendering.Text;

public static class UnicodeWidth
{
    public static int GetWidth(Rune rune)
    {
        var code = rune.Value;

        // Optimized check for common ASCII range
        if (code >= 0x20 && code <= 0x7e) return 1;
        if (code == 0) return 0;

        // Combining characters, non-spacing marks
        if (IsCombining(code)) return 0;

        // East Asian Width: Wide (W) or Fullwidth (F)
        if (IsWide(code)) return 2;

        return 1;
    }

    public static int GetWidth(string cluster)
    {
        if (string.IsNullOrEmpty(cluster)) return 0;

        // The width of a grapheme cluster is generally the width of its base character.
        // We use the first Rune of the cluster to determine its base width.
        return GetWidth(Rune.GetRuneAt(cluster, 0));
    }

    static bool IsCombining(int code) =>
        // General categories: NonSpacingMark, EnclosingMark, SpacingCombiningMark
        // This is a simplified check for common ranges.
        (code >= 0x0300 && code <= 0x036F) || (code >= 0x1DC0 && code <= 0x1DFF) ||
        (code >= 0x20D0 && code <= 0x20FF) || (code >= 0xFE20 && code <= 0xFE2F);

    static bool IsWide(int code)
    {
        // Emoji Wide ranges (approximate for most modern terminals)
        if ((code >= 0x1F300 && code <= 0x1F64F) || // Misc Symbols and Pictographs
            (code >= 0x1F680 && code <= 0x1F6FF) || // Transport and Map
            (code >= 0x1F900 && code <= 0x1F9FF) || // Supplemental Symbols and Pictographs
            (code >= 0x2600 && code <= 0x26FF)) // Misc Symbols
            return true;

        // Simplified East Asian Wide/Fullwidth check
        return code >= 0x1100 && (code <= 0x115f || // Hangul Jamo
                                  code == 0x2329 || code == 0x232a ||
                                  (code >= 0x2e80 && code <= 0xa4cf &&
                                   code != 0x303f) || // CJK ... Yi
                                  (code >= 0xac00 && code <= 0xd7a3) || // Hangul Syllables
                                  (code >= 0xf900 &&
                                   code <= 0xfaff) || // CJK Compatibility Ideographs
                                  (code >= 0xfe10 && code <= 0xfe19) || // Vertical forms
                                  (code >= 0xfe30 && code <= 0xfe6f) || // CJK Compatibility Forms
                                  (code >= 0xff00 && code <= 0xff60) || // Fullwidth Forms
                                  (code >= 0xffe0 && code <= 0xffe6) ||
                                  (code >= 0x20000 && code <= 0x2fffd) ||
                                  (code >= 0x30000 && code <= 0x3fffd));
    }
}