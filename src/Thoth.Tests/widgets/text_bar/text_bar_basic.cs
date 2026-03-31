using System.Text;
using Shouldly;
using Thoth.Terminal.Raw.Egress;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_bar_rendering;

public class text_bar_basic : IAsyncLifetime
{
    ScreenBuffer _buffer = null!;
    RenderContext _context = null!;

    public Task InitializeAsync()
    {
        _buffer = new(20, 1);
        _context = new(new(new Screen()));
        var canvas = new Canvas(_buffer, new(0, 0, 20, 1), _context);

        var textBar = new TextBar
                      {
                          LeftTitle = "L",
                          CenterTitle = "C",
                          RightTitle = "R",
                          Line = "-"
                      };

        textBar.GetScribe().Draw(canvas);
        _buffer.WriteTerminalSnapshotSvg("text_bar_basic.titles.svg");
        _buffer.WriteLayoutDebugSvg(textBar, 20, 1, "text_bar_basic.titles.svg");

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void contains_expected_layout()
    {
        var sb = new StringBuilder();
        for (var x = 0; x < 20; x++)
        {
            var cell = _buffer.GetCell(x, 0);
            sb.Append((char)cell.GlyphId);
        }

        sb.ToString().ShouldBe("L--------C--------R-");
    }
}
