using System.Text;
using Shouldly;
using Thoth.Rendering.Text;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_layout;

public class reflow_keeps_single_long_token_on_line : IAsyncLifetime
{
    TextLayout _sut = null!;

    public Task InitializeAsync()
    {
        var tokenizer = new TextTokenizer();
        _sut = new TextLayout();
        _sut.Initialize(tokenizer.Tokenize([Run("superword")]));
        _sut.Reflow(3, TextOverflow.Wrap);

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void line_estimated_width_is_9()
    {
        _sut.Lines.Count.ShouldBe(1);
        _sut.Lines[0].ShouldBe(new TextLine(0, 1, 9));
    }

    static TextTokenizer.TextRun Run(string text)
    {
        return new(Encoding.UTF8.GetBytes(text));
    }
}
