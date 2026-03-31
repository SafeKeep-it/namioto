using System.Text;
using Shouldly;
using Thoth.Rendering.Text;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_layout;

public class apply_token_delta_splices_tokens : IAsyncLifetime
{
    TextLayout _sut = null!;
    TokenDelta _expected;

    public Task InitializeAsync()
    {
        var tokenizer = new TextTokenizer();
        const string before = "hello world";
        const string after = "hello brave world";

        var initial = tokenizer.Tokenize([Run(before)]);
        var current = initial.Tokens;
        var edit = new TextEdit(TextEditKind.Insert, 1, 0, 0, 0);
        var delta = tokenizer.ApplyEdit([Run(after)], edit, current);

        _expected = tokenizer.Tokenize([Run(after)]);
        _sut = new TextLayout();
        _sut.Initialize(initial);
        _sut.ApplyTokenDelta(delta);

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void token_sequence_matches_full_retokenization()
    {
        _sut.Tokens.Count.ShouldBe(_expected.Tokens.Count);
        for (var i = 0; i < _expected.Tokens.Count; i++)
            _sut.Tokens[i].ShouldBe(_expected.Tokens[i]);
    }

    static TextTokenizer.TextRun Run(string text)
    {
        return new(Encoding.UTF8.GetBytes(text));
    }
}
