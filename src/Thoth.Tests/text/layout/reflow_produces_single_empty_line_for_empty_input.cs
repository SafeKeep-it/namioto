using System.Text;
using Shouldly;
using Thoth.Rendering.Text;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_layout;

public class reflow_produces_single_empty_line_for_empty_input : IAsyncLifetime
{
    TextLayout _sut = null!;

    public Task InitializeAsync()
    {
        var tokenizer = new TextTokenizer();
        _sut = new TextLayout();
        _sut.Initialize(tokenizer.Tokenize([Run("")]));
        _sut.Reflow(4, TextOverflow.Wrap);

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void line_count_is_1()
    {
        _sut.Lines.Count.ShouldBe(1);
        _sut.Lines[0].ShouldBe(new TextLine(0, 0, 0));
    }

    static TextTokenizer.TextRun Run(string text)
    {
        return new(Encoding.UTF8.GetBytes(text));
    }
}
