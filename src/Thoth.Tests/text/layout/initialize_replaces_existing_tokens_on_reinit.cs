using System.Text;
using Shouldly;
using Thoth.Rendering.Text;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_layout;

public class initialize_replaces_existing_tokens_on_reinit : IAsyncLifetime
{
    TextLayout _sut = null!;
    TokenDelta _secondDelta;

    public Task InitializeAsync()
    {
        var tokenizer = new TextTokenizer();
        _sut = new TextLayout();

        _sut.Initialize(tokenizer.Tokenize([Run("alpha beta")]));
        _secondDelta = tokenizer.Tokenize([Run("z")]);
        _sut.Initialize(_secondDelta);

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void token_count_matches_second_initialization()
    {
        _sut.Tokens.Count.ShouldBe(_secondDelta.Tokens.Count);
        for (var i = 0; i < _secondDelta.Tokens.Count; i++)
            _sut.Tokens[i].ShouldBe(_secondDelta.Tokens[i]);
    }

    static TextTokenizer.TextRun Run(string text)
    {
        return new(Encoding.UTF8.GetBytes(text));
    }
}
