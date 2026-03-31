using System.Text;
using Shouldly;
using Thoth.Tests.utilities;
using Thoth.Terminal.Raw.Egress;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.stack_panel_rendering;

public class stack_panel_basic : IAsyncLifetime
{
    ScreenBuffer _buffer = null!;
    public Task InitializeAsync()
    {
        _buffer = new(8, 3);

        var panel = new StackPanel();
        panel.Items.Add(new FixedHeightWidget('A', 1));
        panel.Items.Add(new FixedHeightWidget('B', 2));

        tree_render_harness.Render(panel, _buffer);

        _buffer.WriteTerminalSnapshotSvg("stack_panel_basic.vertical_layout.svg");
        _buffer.WriteLayoutDebugSvg(panel, 8, 3, "stack_panel_basic.vertical_layout.svg");

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void renders_children_from_top_to_bottom()
    {
        row(0).ShouldBe("AAAAAAAA");
        row(1).ShouldBe("BBBBBBBB");
        row(2).ShouldBe("BBBBBBBB");
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

    sealed class FixedHeightWidget(char glyph, int height) : TestWidgetBase
    {
        public override Size Measure(SizeConstraint constraint) => new(constraint.MaxWidth, height);

        public override void Render(Canvas canvas)
        {
            canvas.Fill(0, 0, canvas.Width, canvas.Height, new(glyph), new());
        }
    }
}
