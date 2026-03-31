using System.Diagnostics;
using Shouldly;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_block_rendering;

public class animation_snapshot_steps
{
    [Fact]
    public void writes_svg_regression_strip_for_marquee_overflow()
    {
        var block = new TextBlock
                    {
                        Overflow = TextOverflow.Marquee,
                        MarqueeSpeed = 3,
                        ForegroundColor = new(235, 235, 235),
                        BackgroundColor = new(22, 22, 30),
                        Text = "Marquee overflow travels right to left"
                    };

        const int width = 16;
        var startedAt = Stopwatch.GetTimestamp();
        var interval = Math.Max(1L, Stopwatch.Frequency / Math.Max(1, block.MarqueeSpeed));
        block.UpdateAnimation(startedAt);

        var steps = new List<terminal_snapshot_assertions.AnimationStep>(14);
        for (var i = 0; i < 14; i++)
        {
            var snapshot = JsonTerminal.Capture(Render(block, width));
            steps.Add(new($"step {i:00}", snapshot));
            block.UpdateAnimation(startedAt + ((i + 1L) * interval));
        }

        terminal_snapshot_assertions.WriteAnimationStepsSvg(
            "text_block_overflow.marquee.animation.svg",
            steps,
            new
            {
                control = "TextBlock",
                overflow = block.Overflow.ToString(),
                speed = block.MarqueeSpeed,
                width
            });

        steps.Count.ShouldBe(14);
    }

    static ScreenBuffer Render(TextBlock block, int width)
    {
        var root = new Screen();
        var context = new RenderContext(new UiContext(root));
        var buffer = new ScreenBuffer(width, 1);
        var canvas = new Canvas(buffer, new(0, 0, width, 1), context, frameNumber: 1);
        block.GetRenderer().Measure(new(width, 1));
        block.GetRenderer().Arrange(new(0, 0, width, 1));
        block.GetScribe().Draw(canvas);
        return buffer;
    }
}
