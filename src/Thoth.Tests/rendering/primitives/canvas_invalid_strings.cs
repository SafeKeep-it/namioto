using Shouldly;
using Thoth.Terminal.Raw.Egress;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.canvas_rendering;

public class canvas_invalid_strings : IAsyncLifetime
{
    ScreenBuffer _buffer = null!;
    Canvas _canvas;
    RenderContext _context = null!;
    Style _style;

    public Task InitializeAsync()
    {
        _buffer = new(20, 5);
        var scribe = new TerminalScribe(new MockTerminal());
        _context = new(new(new Screen()));
        _canvas = new(_buffer, new(0, 0, 20, 5), _context);
        _style = new(Color.White, Color.Black);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void handles_unpaired_high_surrogate()
    {
        // \uD83D is high surrogate for many emojis, but here it's alone
        var invalid = "A\uD83DB";

        _canvas.DrawString(0, 0, invalid, _style);

        _buffer.GetCell(0, 0).GlyphId.ShouldBe('A');
        // The current implementation skips invalid sequences (span = span[1..])
        // So 'B' should follow 'A' at x=1
        _buffer.GetCell(1, 0).GlyphId.ShouldBe('B');
    }

    [Fact]
    public void handles_unpaired_low_surrogate()
    {
        var invalid = "A\uDE00B";

        _canvas.DrawString(0, 0, invalid, _style);

        _buffer.GetCell(0, 0).GlyphId.ShouldBe('A');
        _buffer.GetCell(1, 0).GlyphId.ShouldBe('B');
    }

    [Fact]
    public void handles_null_bytes_and_control_chars()
    {
        // Null byte (0) is preserved but width is 0, so it doesn't advance x
        // Control chars (1-31) should be skipped
        var text = "A\0\x01";

        _canvas.DrawString(0, 0, text, _style);

        _buffer.GetCell(0, 0).GlyphId.ShouldBe('A');

        // \0 at x=1, width 0
        _buffer.GetCell(1, 0).GlyphId.ShouldBe(0);
        _buffer.GetCell(1, 0).Width.ShouldBe((byte)0);

        // \x01 skipped
    }

    [Fact]
    public void handles_empty_strings()
    {
        _canvas.DrawString(0, 0, "", _style);
        // Should not throw, should not write
        for (var x = 0; x < 20; x++) _buffer.GetCell(x, 0).GlyphId.ShouldBe(0);
    }
}