using System.Text;
using Shouldly;
using Thoth.Rendering.Text;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_layout;

public class reflow_breaks_line_when_max_width_is_exceeded : IAsyncLifetime
{
    TextLayout _sut = null!;

    public Task InitializeAsync()
    {
        var tokenizer = new TextTokenizer();
        _sut = new TextLayout();
        _sut.Initialize(tokenizer.Tokenize([Run("ab cd")]));
        _sut.Reflow(3, TextOverflow.Wrap);

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void line_count_is_2()
    {
        _sut.Lines.Count.ShouldBe(2);
        _sut.Lines[0].ShouldBe(new TextLine(0, 2, 3));
        _sut.Lines[1].ShouldBe(new TextLine(2, 1, 2));
    }

    static TextTokenizer.TextRun Run(string text)
    {
        return new(Encoding.UTF8.GetBytes(text));
    }
}
