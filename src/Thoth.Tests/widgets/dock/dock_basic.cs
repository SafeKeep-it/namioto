using System.Text;
using Shouldly;
using Thoth.Rendering;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.dock_rendering;

public class dock_basic : IAsyncLifetime
{
    ScreenBuffer _buffer = null!;
    Dock _dock = null!;
    TextBar _content = null!;
    FrameLayoutState _layout = null!;

    public Task InitializeAsync()
    {
        _buffer = new(10, 3);

        _content = new TextBar { LeftTitle = "DOCK", Line = "-" };
        _dock = new Dock { Position = DockPosition.Top, MaximumHeight = 1, Content = _content };

        _layout = tree_render_harness.Render(_dock, _buffer);

        _buffer.WriteTerminalSnapshotSvg("dock_basic.max_height.svg");
        _buffer.WriteLayoutDebugSvg(_dock, 10, 3, "dock_basic.max_height.svg");

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void renders_content_on_first_row_with_max_height()
    {
        row(0).ShouldBe("DOCK------");
        row(1).ShouldBe("          ");
        row(2).ShouldBe("          ");
    }

    [Fact]
    public void arranges_content_to_single_row()
    {
        _layout.TryGetRect(_content, out var rect).ShouldBeTrue();
        rect.ShouldBe(new Rect(0, 0, 10, 1));
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
