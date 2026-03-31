using Shouldly;
using Thoth.Rendering.Text;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_layout;

public class initialize_copies_tokens_from_delta : IAsyncLifetime
{
    TextLayout _sut = null!;

    public Task InitializeAsync()
    {
        _sut = new TextLayout();

        var sourceTokens = new List<TextToken>
        {
            new(0, 0, 1, 1, TokenKind.Word)
        };

        _sut.Initialize(new(sourceTokens, 0, 0));
        sourceTokens[0] = new(0, 0, 1, 9, TokenKind.Punctuation);

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void token_estimated_width_is_1()
    {
        _sut.Tokens.Count.ShouldBe(1);
        _sut.Tokens[0].EstimatedWidth.ShouldBe(1);
        _sut.Tokens[0].Kind.ShouldBe(TokenKind.Word);
    }
}
