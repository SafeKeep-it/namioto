using System.Text;
using Shouldly;
using Thoth.Rendering.Text;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_metrics;

public class text_tokenizer_lexical
{
    [Theory]
    [MemberData(nameof(LexicalCases))]
    public void tokenizes_lexical_categories_from_single_run(string text,
                                                             TokenKind[] expectedKinds,
                                                             int[] expectedWidths,
                                                             int[] expectedByteLengths)
    {
        var tokenizer = new TextTokenizer();
        var delta = tokenizer.Tokenize([Run(text)]);

        delta.ReplaceTokenStart.ShouldBe(0);
        delta.ReplaceTokenCount.ShouldBe(0);
        AssertTokens(delta, expectedKinds, expectedWidths, expectedByteLengths);
    }

    public static IEnumerable<object[]> LexicalCases()
    {
        yield return
        [
            "hello world",
            new[] { TokenKind.Word, TokenKind.Separator, TokenKind.Word },
            new[] { 5, 1, 5 },
            new[] { 5, 1, 5 }
        ];

        yield return
        [
            "a\u00A0b",
            new[] { TokenKind.Word, TokenKind.NonBreakSeparator, TokenKind.Word },
            new[] { 1, 1, 1 },
            new[] { 1, 2, 1 }
        ];

        yield return
        [
            "a \u00A0 b",
            new[] { TokenKind.Word, TokenKind.Separator, TokenKind.NonBreakSeparator, TokenKind.Separator, TokenKind.Word },
            new[] { 1, 1, 1, 1, 1 },
            new[] { 1, 1, 2, 1, 1 }
        ];

        yield return
        [
            "cafe 世界",
            new[] { TokenKind.Word, TokenKind.Separator, TokenKind.Word },
            new[] { 4, 1, 4 },
            new[] { 4, 1, 6 }
        ];

        yield return
        [
            "hello?!",
            new[] { TokenKind.Word },
            new[] { 7 },
            new[] { 7 }
        ];

        yield return
        [
            "a,b;c",
            new[] { TokenKind.Word },
            new[] { 5 },
            new[] { 5 }
        ];

        yield return
        [
            "a - b",
            new[] { TokenKind.Word, TokenKind.Separator, TokenKind.Punctuation, TokenKind.Separator, TokenKind.Word },
            new[] { 1, 1, 1, 1, 1 },
            new[] { 1, 1, 1, 1, 1 }
        ];

        yield return
        [
            "3.14",
            new[] { TokenKind.Word },
            new[] { 4 },
            new[] { 4 }
        ];

        yield return
        [
            "1e-3",
            new[] { TokenKind.Word },
            new[] { 4 },
            new[] { 4 }
        ];
    }

    static void AssertTokens(TokenDelta delta,
                             IReadOnlyList<TokenKind> expectedKinds,
                             IReadOnlyList<int> expectedWidths,
                             IReadOnlyList<int> expectedByteLengths)
    {
        delta.Tokens.Count.ShouldBe(expectedKinds.Count);
        expectedWidths.Count.ShouldBe(expectedKinds.Count);
        expectedByteLengths.Count.ShouldBe(expectedKinds.Count);

        for (var i = 0; i < expectedKinds.Count; i++)
        {
            delta.Tokens[i].Kind.ShouldBe(expectedKinds[i]);
            delta.Tokens[i].EstimatedWidth.ShouldBe(expectedWidths[i]);
            delta.Tokens[i].ByteLength.ShouldBe(expectedByteLengths[i]);
        }
    }

    static TextTokenizer.TextRun Run(string text)
    {
        return new(Encoding.UTF8.GetBytes(text));
    }
}

public class text_tokenizer_apply_edit
{
    [Theory]
    [MemberData(nameof(ApplyEditCases))]
    public void apply_edit_returns_minimal_token_patch(string before,
                                                       string after,
                                                       TextEdit edit,
                                                       int expectedReplaceStart,
                                                       int expectedReplaceCount,
                                                       TokenKind[] expectedPatchKinds)
    {
        var tokenizer = new TextTokenizer();
        var current = tokenizer.Tokenize([Run(before)]).Tokens;
        var delta = tokenizer.ApplyEdit([Run(after)], edit, current);

        delta.ReplaceTokenStart.ShouldBe(expectedReplaceStart);
        delta.ReplaceTokenCount.ShouldBe(expectedReplaceCount);
        delta.Tokens.Count.ShouldBe(expectedPatchKinds.Length);
        for (var i = 0; i < expectedPatchKinds.Length; i++)
            delta.Tokens[i].Kind.ShouldBe(expectedPatchKinds[i]);
    }

    public static IEnumerable<object[]> ApplyEditCases()
    {
        yield return
        [
            "hello world",
            "hello brave world",
            new TextEdit(TextEditKind.Insert, 1, 0, 0, 0),
            1,
            2,
            new[] { TokenKind.Separator, TokenKind.Word, TokenKind.Separator, TokenKind.Word }
        ];

        yield return
        [
            "alpha beta gamma",
            "alpha gamma",
            new TextEdit(TextEditKind.Delete, 2, 3, 0, 0),
            2,
            3,
            new[] { TokenKind.Word }
        ];

        yield return
        [
            "start foo end",
            "start bar end",
            new TextEdit(TextEditKind.Replace, 2, 2, 0, 3),
            2,
            1,
            new[] { TokenKind.Word }
        ];
    }

    static TextTokenizer.TextRun Run(string text)
    {
        return new(Encoding.UTF8.GetBytes(text));
    }
}

public class text_tokenizer_apply_edit_scope
{
    [Theory]
    [MemberData(nameof(ScopeCases))]
    public void apply_edit_patch_is_scoped_and_reconstructs_expected_tokens(string before,
                                                                             string after,
                                                                             TextEdit edit,
                                                                             int expectedReplaceStart,
                                                                             int expectedReplaceCount,
                                                                             int expectedPatchCount)
    {
        var tokenizer = new TextTokenizer();
        var beforeTokens = tokenizer.Tokenize([Run(before)]).Tokens;
        var delta = tokenizer.ApplyEdit([Run(after)], edit, beforeTokens);
        var fullAfterTokens = tokenizer.Tokenize([Run(after)]).Tokens;

        delta.ReplaceTokenStart.ShouldBe(expectedReplaceStart);
        delta.ReplaceTokenCount.ShouldBe(expectedReplaceCount);
        delta.Tokens.Count.ShouldBe(expectedPatchCount);

        var reconstructed = ApplyPatch(beforeTokens, delta);
        reconstructed.Count.ShouldBe(fullAfterTokens.Count);
        for (var i = 0; i < fullAfterTokens.Count; i++)
            reconstructed[i].ShouldBe(fullAfterTokens[i]);
    }

    public static IEnumerable<object[]> ScopeCases()
    {
        yield return
        [
            "alpha beta gamma delta",
            "alpha BETA gamma delta",
            new TextEdit(TextEditKind.Replace, 2, 2, 0, 4),
            2,
            1,
            1
        ];

        yield return
        [
            "hello world again",
            "hello brave world again",
            new TextEdit(TextEditKind.Insert, 1, 0, 0, 0),
            1,
            4,
            6
        ];

        yield return
        [
            "one two three four",
            "one four",
            new TextEdit(TextEditKind.Delete, 2, 5, 0, 0),
            2,
            5,
            1
        ];
    }

    static List<TextToken> ApplyPatch(IReadOnlyList<TextToken> source, TokenDelta delta)
    {
        var result = new List<TextToken>(source.Count - delta.ReplaceTokenCount + delta.Tokens.Count);
        for (var i = 0; i < delta.ReplaceTokenStart; i++)
            result.Add(source[i]);

        for (var i = 0; i < delta.Tokens.Count; i++)
            result.Add(delta.Tokens[i]);

        var suffixStart = delta.ReplaceTokenStart + delta.ReplaceTokenCount;
        for (var i = suffixStart; i < source.Count; i++)
            result.Add(source[i]);

        return result;
    }

    static TextTokenizer.TextRun Run(string text)
    {
        return new(Encoding.UTF8.GetBytes(text));
    }
}

public class text_tokenizer_newlines
{
    [Theory]
    [MemberData(nameof(NewLineCases))]
    public void normalizes_newline_token_shapes(string text,
                                                int expectedNewLineByteLength)
    {
        var tokenizer = new TextTokenizer();
        var delta = tokenizer.Tokenize([Run(text)]);

        delta.Tokens.Count.ShouldBe(3);
        delta.Tokens[0].Kind.ShouldBe(TokenKind.Word);
        delta.Tokens[1].Kind.ShouldBe(TokenKind.NewLine);
        delta.Tokens[2].Kind.ShouldBe(TokenKind.Word);
        delta.Tokens[1].EstimatedWidth.ShouldBe(0);
        delta.Tokens[1].ByteLength.ShouldBe(expectedNewLineByteLength);
    }

    public static IEnumerable<object[]> NewLineCases()
    {
        yield return ["one\ntwo", 1];
        yield return ["one\rtwo", 1];
        yield return ["one\r\ntwo", 2];
        yield return ["one\u2028two", 3];
        yield return ["one\u2029two", 3];
    }

    static TextTokenizer.TextRun Run(string text)
    {
        return new(Encoding.UTF8.GetBytes(text));
    }
}

public class text_tokenizer_multi_run
{
    [Theory]
    [MemberData(nameof(MultiRunCases))]
    public void preserves_run_index_across_multiple_runs(TextTokenizer.TextRun[] runs,
                                                         int[] expectedRunIndexes,
                                                         TokenKind[] expectedKinds)
    {
        var tokenizer = new TextTokenizer();
        var delta = tokenizer.Tokenize(runs);

        delta.Tokens.Count.ShouldBe(expectedRunIndexes.Length);
        for (var i = 0; i < expectedRunIndexes.Length; i++)
        {
            delta.Tokens[i].RunIndex.ShouldBe(expectedRunIndexes[i]);
            delta.Tokens[i].Kind.ShouldBe(expectedKinds[i]);
        }
    }

    public static IEnumerable<object[]> MultiRunCases()
    {
        yield return
        [
            new[]
            {
                Run("abc"),
                Run(" def")
            },
            new[] { 0, 1, 1 },
            new[] { TokenKind.Word, TokenKind.Separator, TokenKind.Word }
        ];

        yield return
        [
            new[]
            {
                Run("one"),
                Run("\n"),
                Run("two")
            },
            new[] { 0, 1, 2 },
            new[] { TokenKind.Word, TokenKind.NewLine, TokenKind.Word }
        ];

        yield return
        [
            new[]
            {
                Run("one\r"),
                Run("\ntwo")
            },
            new[] { 0, 0, 1 },
            new[] { TokenKind.Word, TokenKind.NewLine, TokenKind.Word }
        ];
    }

    static TextTokenizer.TextRun Run(string text)
    {
        return new(Encoding.UTF8.GetBytes(text));
    }
}
