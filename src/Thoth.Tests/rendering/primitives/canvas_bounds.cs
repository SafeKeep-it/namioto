using System.Text;
using Thoth.Terminal.Raw.Egress;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.canvas_rendering;

public class canvas_bounds : IAsyncLifetime
{
    ScreenBuffer _buffer = null!;

    public Task InitializeAsync()
    {
        _buffer = new(10, 10);
        var scribe = new TerminalScribe(new MockTerminal());
        var context = new RenderContext(new(new Screen()));
        var canvas = new Canvas(_buffer, new(2, 2, 5, 5), context);
        var style = new Style(new Color(255, 255, 255), new Color(0, 0, 0));

        canvas.PutGlyph(-1, 0, (Rune)'X', style);
        canvas.PutGlyph(5, 0, (Rune)'Y', style);
        canvas.PutGlyph(0, -1, (Rune)'Z', style);
        canvas.PutGlyph(0, 5, (Rune)'W', style);

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;
}