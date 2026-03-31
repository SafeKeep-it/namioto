using System.Text;
using Shouldly;
using Thoth.Terminal.Raw.Egress;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.canvas_rendering;

public class canvas_basic : IAsyncLifetime
{
    ScreenBuffer _buffer = null!;
    Cell _cell;

    public Task InitializeAsync()
    {
        _buffer = new(10, 10);
        var scribe = new TerminalScribe(new MockTerminal());
        var context = new RenderContext(new(new Screen()));
        var canvas = new Canvas(_buffer, new(2, 2, 5, 5), context);
        var style = new Style(new Color(255, 255, 255), new Color(0, 0, 0));

        canvas.PutGlyph(0, 0, (Rune)'X', style);
        _cell = _buffer.GetCell(2, 2);

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void cell_contains_correct_char()
    {
        _cell.GlyphId.ShouldBe('X');
    }
}