using System.Text;
using Shouldly;
using Thoth.Rendering.Text;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_layout;

public class reflow_breaks_line_on_newline_token : IAsyncLifetime
{
    TextLayout _sut = null!;

    public Task InitializeAsync()
    {
        var tokenizer = new TextTokenizer();
        _sut = new TextLayout();
        _sut.Initialize(tokenizer.Tokenize([Run("ab\ncd")]));
        _sut.Reflow(20, TextOverflow.Wrap);

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void newline_token_is_not_part_of_any_line()
    {
        _sut.Lines.Count.ShouldBe(2);
        _sut.Lines[0].ShouldBe(new TextLine(0, 1, 2));
        _sut.Lines[1].ShouldBe(new TextLine(2, 1, 2));
    }

    static TextTokenizer.TextRun Run(string text)
    {
        return new(Encoding.UTF8.GetBytes(text));
    }
}
