using System.Buffers;
using System.Globalization;
using System.Text;

namespace Thoth.Rendering.Text;

public sealed class TextTokenizer
{
    readonly IWidthProvider _widthProvider;
    readonly List<TextToken> _scratchTokens = [];

    public TextTokenizer(IWidthProvider? widthProvider = null)
    {
        _widthProvider = widthProvider ?? new UnicodeWidthProvider();
    }

    public TokenDelta Tokenize(IReadOnlyList<TextRun> runs)
    {
        var estimatedCapacity = EstimateTokenCapacity(runs);
        _scratchTokens.Clear();
        if (_scratchTokens.Capacity < estimatedCapacity)
            _scratchTokens.Capacity = estimatedCapacity;

        TokenizeInto(runs, _scratchTokens);
        return new(new List<TextToken>(_scratchTokens), 0, 0);
    }

    public TokenDelta ApplyEdit(IReadOnlyList<TextRun> runs,
                                in TextEdit edit,
                                IReadOnlyList<TextToken> currentTokens)
    {
        var estimatedCapacity = Math.Max(currentTokens.Count, EstimateTokenCapacity(runs));
        _scratchTokens.Clear();
        if (_scratchTokens.Capacity < estimatedCapacity)
            _scratchTokens.Capacity = estimatedCapacity;

        TokenizeInto(runs, _scratchTokens);
        var fullTokens = _scratchTokens;

        var affectedStart = Math.Clamp(edit.AffectedTokenStart, 0, currentTokens.Count);
        var affectedEnd = edit.AffectedTokenEnd < affectedStart
            ? affectedStart - 1
            : Math.Clamp(edit.AffectedTokenEnd, affectedStart, Math.Max(affectedStart - 1, currentTokens.Count - 1));

        var runByteDeltas = ComputeRunByteDeltas(runs, currentTokens);

        var prefix = 0;
        var maxPrefix = Math.Min(affectedStart, Math.Min(currentTokens.Count, fullTokens.Count));
        while (prefix < maxPrefix && PrefixCompatible(currentTokens[prefix], fullTokens[prefix]))
            prefix++;

        var suffixOldStart = Math.Max(prefix, affectedEnd + 1);
        var maxSuffixByOld = Math.Max(0, currentTokens.Count - suffixOldStart);
        var maxSuffix = Math.Min(maxSuffixByOld, Math.Max(0, fullTokens.Count - prefix));

        var suffix = 0;
        while (suffix < maxSuffix)
        {
            var oldToken = currentTokens[currentTokens.Count - 1 - suffix];
            var newToken = fullTokens[fullTokens.Count - 1 - suffix];
            if (oldToken.RunIndex >= 0
                && oldToken.RunIndex < runByteDeltas.Length
                && runByteDeltas[oldToken.RunIndex] != 0)
                break;

            if (!SuffixCompatible(oldToken, newToken, runByteDeltas))
                break;

            suffix++;
        }
        var replaceStart = prefix;
        var replaceCount = Math.Max(0, currentTokens.Count - prefix - suffix);
        var insertCount = Math.Max(0, fullTokens.Count - prefix - suffix);

        if (insertCount == 0)
            return new(Array.Empty<TextToken>(), replaceStart, replaceCount);

        var patchTokens = new TextToken[insertCount];
        for (var i = 0; i < insertCount; i++)
            patchTokens[i] = fullTokens[prefix + i];

        return new(patchTokens, replaceStart, replaceCount);
    }

    void TokenizeInto(IReadOnlyList<TextRun> runs, List<TextToken> tokens)
    {
        tokens.Clear();
        var consumeLeadingLfFromPreviousCr = false;

        for (var runIndex = 0; runIndex < runs.Count; runIndex++)
        {
            var bytes = runs[runIndex].Utf8.Span;
            if (bytes.Length == 0) continue;

            tokenize_run(bytes, runIndex, tokens, ref consumeLeadingLfFromPreviousCr);
        }
    }

    void tokenize_run(ReadOnlySpan<byte> bytes,
                      int runIndex,
                      List<TextToken> tokens,
                      ref bool consumeLeadingLfFromPreviousCr)
    {
        var i = skip_leading_lf_if_needed(bytes, consumeLeadingLfFromPreviousCr);
        consumeLeadingLfFromPreviousCr = false;

        while (i < bytes.Length)
        {
            if (TryConsumeNewLine(bytes, i, out var newlineBytes, out var trailingCr))
            {
                tokens.Add(new(runIndex, i, newlineBytes, 0, TokenKind.NewLine));
                i += newlineBytes;
                consumeLeadingLfFromPreviousCr = trailingCr;
                continue;
            }

            i = tokenize_non_newline(bytes, i, runIndex, tokens);
        }
    }

    int tokenize_non_newline(ReadOnlySpan<byte> bytes,
                             int index,
                             int runIndex,
                             List<TextToken> tokens)
    {
        read_rune(bytes, index, out var rune, out _);
        return Rune.IsWhiteSpace(rune)
            ? consume_whitespace_token(bytes, index, runIndex, tokens)
            : consume_text_token(bytes, index, runIndex, tokens);
    }

    int consume_whitespace_token(ReadOnlySpan<byte> bytes,
                                 int tokenStart,
                                 int runIndex,
                                 List<TextToken> tokens)
    {
        read_rune(bytes, tokenStart, out var firstRune, out _);
        var kind = IsNonBreakSpace(firstRune)
            ? TokenKind.NonBreakSeparator
            : TokenKind.Separator;

        var i = tokenStart;
        var tokenWidth = 0;

        while (i < bytes.Length)
        {
            if (TryConsumeNewLine(bytes, i, out _, out _)) break;

            read_rune(bytes, i, out var rune, out var consumed);
            if (!Rune.IsWhiteSpace(rune)) break;

            var scalarKind = IsNonBreakSpace(rune)
                ? TokenKind.NonBreakSeparator
                : TokenKind.Separator;
            if (scalarKind != kind) break;

            tokenWidth += MeasureRuneWidth(rune);
            i += consumed;
        }

        tokens.Add(new(runIndex, tokenStart, i - tokenStart, tokenWidth, kind));
        return i;
    }

    int consume_text_token(ReadOnlySpan<byte> bytes,
                           int tokenStart,
                           int runIndex,
                           List<TextToken> tokens)
    {
        var i = tokenStart;
        var tokenWidth = 0;
        var punctuationOnly = true;

        while (i < bytes.Length)
        {
            if (TryConsumeNewLine(bytes, i, out _, out _)) break;

            read_rune(bytes, i, out var rune, out var consumed);
            if (Rune.IsWhiteSpace(rune)) break;

            punctuationOnly &= IsPunctuation(rune);
            tokenWidth += MeasureRuneWidth(rune);
            i += consumed;
        }

        if (i > tokenStart)
        {
            tokens.Add(new(runIndex,
                           tokenStart,
                           i - tokenStart,
                           tokenWidth,
                           punctuationOnly ? TokenKind.Punctuation : TokenKind.Word));
        }

        return i;
    }

    static int skip_leading_lf_if_needed(ReadOnlySpan<byte> bytes, bool consumeLeadingLfFromPreviousCr)
    {
        if (!consumeLeadingLfFromPreviousCr) return 0;
        return bytes[0] == (byte)'\n' ? 1 : 0;
    }

    static void read_rune(ReadOnlySpan<byte> bytes, int index, out Rune rune, out int consumed)
    {
        var status = Rune.DecodeFromUtf8(bytes[index..], out rune, out consumed);
        if (status == OperationStatus.Done && consumed > 0) return;

        consumed = 1;
        rune = new Rune('?');
    }

    static int EstimateTokenCapacity(IReadOnlyList<TextRun> runs)
    {
        var totalBytes = 0;
        for (var i = 0; i < runs.Count; i++)
            totalBytes += runs[i].Utf8.Length;

        if (totalBytes <= 0) return 0;
        return Math.Max(4, Math.Min(totalBytes, totalBytes / 2 + 16));
    }

    static int[] ComputeRunByteDeltas(IReadOnlyList<TextRun> runs, IReadOnlyList<TextToken> currentTokens)
    {
        var maxRunIndex = runs.Count - 1;
        for (var i = 0; i < currentTokens.Count; i++)
            maxRunIndex = Math.Max(maxRunIndex, currentTokens[i].RunIndex);

        var oldLengths = new int[maxRunIndex + 1];
        var newLengths = new int[maxRunIndex + 1];

        for (var i = 0; i < currentTokens.Count; i++)
        {
            var token = currentTokens[i];
            var end = token.ByteStart + token.ByteLength;
            if (end > oldLengths[token.RunIndex])
                oldLengths[token.RunIndex] = end;
        }

        for (var i = 0; i < runs.Count; i++)
            newLengths[i] = runs[i].Utf8.Length;

        var deltas = new int[maxRunIndex + 1];
        for (var i = 0; i < deltas.Length; i++)
            deltas[i] = newLengths[i] - oldLengths[i];

        return deltas;
    }

    static bool PrefixCompatible(in TextToken left, in TextToken right)
    {
        return left.RunIndex == right.RunIndex
               && left.ByteStart == right.ByteStart
               && left.ByteLength == right.ByteLength
               && left.EstimatedWidth == right.EstimatedWidth
               && left.Kind == right.Kind;
    }

    static bool SuffixCompatible(in TextToken oldToken, in TextToken newToken, IReadOnlyList<int> runByteDeltas)
    {
        if (oldToken.RunIndex != newToken.RunIndex) return false;
        if (oldToken.ByteLength != newToken.ByteLength) return false;
        if (oldToken.EstimatedWidth != newToken.EstimatedWidth) return false;
        if (oldToken.Kind != newToken.Kind) return false;

        var runIndex = oldToken.RunIndex;
        var shift = runIndex >= 0 && runIndex < runByteDeltas.Count ? runByteDeltas[runIndex] : 0;
        return oldToken.ByteStart + shift == newToken.ByteStart;
    }

    static bool TryConsumeNewLine(ReadOnlySpan<byte> bytes,
                                  int index,
                                  out int consumed,
                                  out bool trailingCr)
    {
        consumed = 0;
        trailingCr = false;

        if (index >= bytes.Length) return false;

        if (bytes[index] == (byte)'\r')
        {
            if (index + 1 < bytes.Length && bytes[index + 1] == (byte)'\n')
            {
                consumed = 2;
                return true;
            }

            consumed = 1;
            trailingCr = true;
            return true;
        }

        if (bytes[index] == (byte)'\n')
        {
            consumed = 1;
            return true;
        }

        if (index + 2 < bytes.Length
            && bytes[index] == 0xE2
            && bytes[index + 1] == 0x80
            && (bytes[index + 2] == 0xA8 || bytes[index + 2] == 0xA9))
        {
            consumed = 3;
            return true;
        }

        return false;
    }

    static bool IsNonBreakSpace(Rune rune)
    {
        return rune.Value is 0x00A0 or 0x202F or 0x2007;
    }

    static bool IsPunctuation(Rune rune)
    {
        return Rune.GetUnicodeCategory(rune) is
            UnicodeCategory.ConnectorPunctuation or
            UnicodeCategory.DashPunctuation or
            UnicodeCategory.OpenPunctuation or
            UnicodeCategory.ClosePunctuation or
            UnicodeCategory.InitialQuotePunctuation or
            UnicodeCategory.FinalQuotePunctuation or
            UnicodeCategory.OtherPunctuation;
    }

    int MeasureRuneWidth(Rune rune)
    {
        Span<char> buffer = stackalloc char[2];
        var written = rune.EncodeToUtf16(buffer);
        return _widthProvider.GetWidth(buffer[..written]);
    }

    public readonly record struct TextRun(ReadOnlyMemory<byte> Utf8);
}

public readonly record struct TextToken(int RunIndex,
                                        int ByteStart,
                                        int ByteLength,
                                        int EstimatedWidth,
                                        TokenKind Kind);

public readonly record struct TokenDelta(IReadOnlyList<TextToken> Tokens,
                                         int ReplaceTokenStart,
                                         int ReplaceTokenCount);

public enum TextEditKind
{
    Insert,
    Delete,
    Replace
}

public readonly record struct TextEdit(TextEditKind Kind,
                                       int AffectedTokenStart,
                                       int AffectedTokenEnd,
                                       int StartOffsetInFirstToken,
                                       int EndOffsetInLastToken,
                                       IReadOnlyList<TextTokenizer.TextRun>? InsertedRuns = null);

public enum TokenKind
{
    Word,
    Separator,
    NonBreakSeparator,
    Punctuation,
    NewLine
}
