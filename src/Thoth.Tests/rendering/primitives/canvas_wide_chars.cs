using Shouldly;
using Thoth.Terminal.Raw.Egress;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.canvas_rendering;

public class canvas_wide_chars : IAsyncLifetime
{
    ScreenBuffer _buffer = null!;

    public Task InitializeAsync()
    {
        _buffer = new(5, 1);
        var scribe = new TerminalScribe(new MockTerminal());
        var context = new RenderContext(new(new Screen()));
        var canvas = new Canvas(_buffer, new(0, 0, 5, 1), context);
        var style = new Style(new Color(255, 255, 255), new Color(0, 0, 0));

        canvas.DrawString(0, 0, "中A", style);

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void sets_width_correctly()
    {
        var cell0 = _buffer.GetCell(0, 0);
        cell0.GlyphId.ShouldBe(0x4E2D);
        cell0.Width.ShouldBe((byte)2);

        var cell1 = _buffer.GetCell(1, 0);
        cell1.GlyphId.ShouldBe(0);
        cell1.Width.ShouldBe((byte)0);

        var cell2 = _buffer.GetCell(2, 0);
        cell2.GlyphId.ShouldBe('A');
        cell2.Width.ShouldBe((byte)1);
    }
}