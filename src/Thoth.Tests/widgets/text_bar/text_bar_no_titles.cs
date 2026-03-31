using System.Text;
using Shouldly;
using Thoth.Terminal.Raw.Egress;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_bar_rendering;

public class text_bar_no_titles : IAsyncLifetime
{
    ScreenBuffer _buffer = null!;
    RenderContext _context = null!;

    public Task InitializeAsync()
    {
        _buffer = new(10, 1);
        _context = new(new(new Screen()));
        var canvas = new Canvas(_buffer, new(0, 0, 10, 1), _context);

        var textBar = new TextBar { Line = "=" };

        textBar.GetScribe().Draw(canvas);
        _buffer.WriteTerminalSnapshotSvg("text_bar_no_titles.rule_only.svg");
        _buffer.WriteLayoutDebugSvg(textBar, 10, 1, "text_bar_no_titles.rule_only.svg");

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void renders_full_bar()
    {
        var sb = new StringBuilder();
        for (var x = 0; x < 10; x++)
        {
            var cell = _buffer.GetCell(x, 0);
            sb.Append((char)cell.GlyphId);
        }

        sb.ToString().ShouldBe("==========");
    }
}
