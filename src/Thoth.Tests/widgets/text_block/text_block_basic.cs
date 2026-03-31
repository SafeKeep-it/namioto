using System.Text;
using Shouldly;
using Thoth.Terminal.Raw.Egress;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_block_rendering;

public class text_block_basic : IAsyncLifetime
{
    ScreenBuffer _buffer = null!;
    RenderContext _context = null!;
    TextBlock _textBlock = null!;

    public Task InitializeAsync()
    {
        _buffer = new(10, 2);
        _context = new(new(new Screen()));
        var canvas = new Canvas(_buffer, new(0, 0, 10, 2), _context);

        _textBlock = new()
                     {
                         Align = global::Thoth.Widgets.Align.Right,
                         Text = "Hi"
                     };

        _textBlock.GetRenderer().Measure(new(10, 2));
        _textBlock.GetRenderer().Arrange(new(0, 0, 10, 2));
        ((IWidget)_textBlock).GetScribe().Draw(canvas);

        _buffer.WriteTerminalSnapshotSvg("text_block_basic.right_alignment.svg");
        _buffer.WriteLayoutDebugSvg(_textBlock, 10, 2, "text_block_basic.right_alignment.svg");

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void renders_right_aligned_text_in_snapshot()
    {
        row(0).ShouldBe("        Hi");
    }

    string row(int y)
    {
        var sb = new StringBuilder(_buffer.Width);
        for (var x = 0; x < _buffer.Width; x++)
        {
            var cell = _buffer.GetCell(x, y);
            sb.Append(cell.GlyphId == 0 ? ' ' : (char)cell.GlyphId);
        }

        return sb.ToString();
    }
}
