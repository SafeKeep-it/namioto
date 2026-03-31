using System.Text;
using Shouldly;
using Thoth.Terminal.Raw.Egress;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_bar_rendering;

public class text_bar_overlaps : IAsyncLifetime
{
    ScreenBuffer _buffer = null!;
    RenderContext _context = null!;

    public Task InitializeAsync()
    {
        _buffer = new(10, 1);
        _context = new(new(new Screen()));
        var canvas = new Canvas(_buffer, new(0, 0, 10, 1), _context);

        var textBar = new TextBar
                      {
                          LeftTitle = "LEFT",
                          CenterTitle = "CENTER",
                          RightTitle = "RIGHT",
                          Line = "-"
                      };

        textBar.GetScribe().Draw(canvas);
        _buffer.WriteTerminalSnapshotSvg("text_bar_overlaps.center_right_overlap.svg");
        _buffer.WriteLayoutDebugSvg(textBar, 10, 1, "text_bar_overlaps.center_right_overlap.svg");

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void pushes_center_to_avoid_overlap()
    {
        var sb = new StringBuilder();
        for (var x = 0; x < 10; x++)
        {
            var cell = _buffer.GetCell(x, 0);
            sb.Append(cell.GlyphId != 0 ? (char)cell.GlyphId : ' ');
        }

        sb.ToString().ShouldBe("LEFTRIGHT-");
    }
}
