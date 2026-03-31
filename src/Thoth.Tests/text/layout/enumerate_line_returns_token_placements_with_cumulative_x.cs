using System.Text;
using Shouldly;
using Thoth.Rendering.Text;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_layout;

public class enumerate_line_returns_token_placements_with_cumulative_x : IAsyncLifetime
{
    List<LineTokenPlacement> _placements = null!;

    public Task InitializeAsync()
    {
        var tokenizer = new TextTokenizer();
        var sut = new TextLayout();
        sut.Initialize(tokenizer.Tokenize([Run("a bb")]));
        sut.Reflow(20, TextOverflow.Wrap);
        _placements = [..sut.EnumerateLine(0)];

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void x_positions_are_cumulative()
    {
        _placements.Count.ShouldBe(3);
        _placements[0].TokenIndex.ShouldBe(0);
        _placements[0].X.ShouldBe(0);
        _placements[1].TokenIndex.ShouldBe(1);
        _placements[1].X.ShouldBe(1);
        _placements[2].TokenIndex.ShouldBe(2);
        _placements[2].X.ShouldBe(2);
    }

    static TextTokenizer.TextRun Run(string text)
    {
        return new(Encoding.UTF8.GetBytes(text));
    }
}
