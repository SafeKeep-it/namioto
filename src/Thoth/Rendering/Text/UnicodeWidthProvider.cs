using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Thoth.Rendering.Text;

public sealed class UnicodeWidthProvider : IWidthProvider
{
    readonly Dictionary<int, byte> _runeWidthCache = new(256);

    public UnicodeWidthProvider()
    {
        SeedCommonRuneWidths();
    }

    public byte GetWidth(ReadOnlySpan<char> cluster)
    {
        if (cluster.Length == 0) return 0;

        if (cluster.Length == 1)
        {
            var value = cluster[0];
            if (value <= 0x7f)
                return char.IsControl(value) ? (byte)0 : (byte)1;

            if (!char.IsSurrogate(value))
                return GetOrAddRuneWidth(value);
        }

        if (cluster.Length == 2 && char.IsSurrogatePair(cluster[0], cluster[1]))
            return GetOrAddRuneWidth(new Rune(cluster[0], cluster[1]).Value);

        if (Rune.DecodeFromUtf16(cluster, out var rune, out var _) == OperationStatus.Done)
            return GetOrAddRuneWidth(rune.Value);

        return 1;
    }

    byte GetOrAddRuneWidth(int runeValue)
    {
        if (_runeWidthCache.TryGetValue(runeValue, out var width))
            return width;

        width = (byte)TextMetrics.GetRuneWidth(new Rune(runeValue));
        _runeWidthCache[runeValue] = width;
        return width;
    }

    void SeedCommonRuneWidths()
    {
        Seed("─│┌┐└┘├┤┬┴┼");
        Seed("═║╔╗╚╝╠╣╦╩╬");
        Seed("░▒▓█■");
        Seed("○◉☐☑×");
        Seed("⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏");
    }

    void Seed(string symbols)
    {
        foreach (var symbol in symbols)
            _runeWidthCache[symbol] = (byte)TextMetrics.GetRuneWidth(new Rune(symbol));
    }
}
