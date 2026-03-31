using System.Text;
using Shouldly;
using Thoth.Tests.utilities;
using Thoth.Widgets;
using Thoth.Widgets.Layout;
using WidgetAlign = global::Thoth.Widgets.Layout.Align;

namespace Comptatata.Tests.App.Cli.UI.Thoth.align_rendering;

public class align_left : IAsyncLifetime
{
    ScreenBuffer _buffer = null!;
    WidgetAlign _align = null!;

    public Task InitializeAsync()
    {
        _buffer = new(10, 1);
        _align = new()
                 {
                     HorizontalAlignment = HorizontalAlignment.Left,
                     Content = new FixedGlyphWidget("AB")
                 };

        tree_render_harness.Render(_align, _buffer);

        _buffer.WriteTerminalSnapshotSvg("align_left.left_alignment.svg");
        _buffer.WriteLayoutDebugSvg(_align, 10, 1, "align_left.left_alignment.svg");

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void renders_content_at_left_edge()
    {
        row(0).ShouldBe("AB        ");
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

    sealed class FixedGlyphWidget(string text) : TestWidgetBase
    {
        public override Size Measure(SizeConstraint constraint) => new(text.Length, 1);

        public override void Render(Canvas canvas)
        {
            canvas.DrawString(0, 0, text, new());
        }
    }
}
