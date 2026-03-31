using System.Diagnostics;
using System.Text;
using Shouldly;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_block_rendering;

public class text_block_overflow
{
    [Fact]
    public void wrap_overflow_keeps_multiline_behavior()
    {
        var (buffer, block) = Render(6, 2, TextOverflow.Wrap, "Hello world");

        buffer.WriteTerminalSnapshotSvg("text_block_overflow.wrap.svg");
        buffer.WriteLayoutDebugSvg(block, 6, 2, "text_block_overflow.wrap.svg");

        Row(buffer, 0).ShouldBe("Hello ");
        Row(buffer, 1).ShouldBe("world ");
    }

    [Fact]
    public void clip_overflow_truncates_without_suffix()
    {
        var (buffer, block) = Render(6, 1, TextOverflow.Clip, "Hello world");

        buffer.WriteTerminalSnapshotSvg("text_block_overflow.clip.svg");
        buffer.WriteLayoutDebugSvg(block, 6, 1, "text_block_overflow.clip.svg");

        Row(buffer, 0).ShouldBe("Hello ");
    }

    [Fact]
    public void ellipsis_overflow_truncates_with_ellipsis_suffix()
    {
        var (buffer, block) = Render(6, 1, TextOverflow.Ellipsis, "Hello world");

        buffer.WriteTerminalSnapshotSvg("text_block_overflow.ellipsis.svg");
        buffer.WriteLayoutDebugSvg(block, 6, 1, "text_block_overflow.ellipsis.svg");

        Row(buffer, 0).ShouldBe("Hello…");
    }

    [Fact]
    public void repeated_measure_draw_at_same_width_keeps_same_overflow_output()
    {
        var (first, block) = Render(6, 1, TextOverflow.Ellipsis, "Hello world");
        var second = RenderInto(block, 6, 1);

        Row(first, 0).ShouldBe("Hello…");
        Row(second, 0).ShouldBe("Hello…");
        Row(second, 0).ShouldBe(Row(first, 0));
    }

    [Fact]
    public void marquee_overflow_scrolls_right_to_left()
    {
        var (buffer0, block) = Render(6, 1, TextOverflow.Marquee, "Hello world");
        var start = Stopwatch.GetTimestamp();

        block.UpdateAnimation(start).ShouldBeFalse();
        var row0 = Row(buffer0, 0);

        block.UpdateAnimation(start + (Stopwatch.Frequency / Math.Max(1, block.MarqueeSpeed))).ShouldBeTrue();
        var buffer1 = RenderInto(block, 6, 1);
        var row1 = Row(buffer1, 0);

        row0.ShouldBe("Hello ");
        row1.ShouldStartWith("ello ");
        row1.ShouldNotBe(row0);
    }

    static (ScreenBuffer Buffer, TextBlock Block) Render(int width,
                                                         int height,
                                                         TextOverflow overflow,
                                                         string text)
    {
        var root = new Screen();
        var context = new RenderContext(new UiContext(root));
        var buffer = new ScreenBuffer(width, height);
        var canvas = new Canvas(buffer, new(0, 0, width, height), context, frameNumber: 1);
        var block = new TextBlock
                    {
                        Overflow = overflow,
                        Text = text
                    };

        block.GetRenderer().Measure(new(width, height));
        block.GetRenderer().Arrange(new(0, 0, width, height));
        block.GetScribe().Draw(canvas);
        return (buffer, block);
    }

    static ScreenBuffer RenderInto(TextBlock block, int width, int height)
    {
        var root = new Screen();
        var context = new RenderContext(new UiContext(root));
        var buffer = new ScreenBuffer(width, height);
        var canvas = new Canvas(buffer, new(0, 0, width, height), context, frameNumber: 1);
        block.GetRenderer().Measure(new(width, height));
        block.GetRenderer().Arrange(new(0, 0, width, height));
        block.GetScribe().Draw(canvas);
        return buffer;
    }

    static string Row(ScreenBuffer buffer, int y)
    {
        var sb = new StringBuilder(buffer.Width);
        for (var x = 0; x < buffer.Width; x++)
        {
            var cell = buffer.GetCell(x, y);
            sb.Append(cell.GlyphId == 0 ? ' ' : (char)cell.GlyphId);
        }

        return sb.ToString();
    }
}
