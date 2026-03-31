using System.Diagnostics;
using Shouldly;
using Thoth.Widgets;

namespace Thoth.Tests.widgets.spinner;

public class animation_snapshot_steps
{
    [Fact]
    public void writes_svg_regression_strip_for_kit()
    {
        var spinner = new Spinner
                      {
                          Dial = SpinnerDial.Kit,
                          Speed = 2,
                          LaneWidth = 11,
                          TrailRadius = 2,
                          ForegroundColor = new(220, 70, 70),
                          BackgroundColor = new(20, 20, 20)
                      };

        var startedAt = Stopwatch.GetTimestamp();
        var interval = Math.Max(1L, Stopwatch.Frequency / Math.Max(1, spinner.Speed));
        spinner.UpdateAnimation(startedAt);

        var steps = new List<terminal_snapshot_assertions.AnimationStep>(12);
        for (var i = 0; i < 12; i++)
        {
            var snapshot = JsonTerminal.Capture(Render(spinner, spinner.LaneWidth));
            steps.Add(new($"step {i:00}", snapshot));
            spinner.UpdateAnimation(startedAt + ((i + 1L) * interval));
        }

        terminal_snapshot_assertions.WriteAnimationStepsSvg(
            "spinner.kit.animation.svg",
            steps,
            new
            {
                control = "Spinner",
                dial = spinner.Dial.ToString(),
                speed = spinner.Speed,
                laneWidth = spinner.LaneWidth,
                trailRadius = spinner.TrailRadius
            });

        steps.Count.ShouldBe(12);
    }

    static ScreenBuffer Render(Spinner spinner, int width)
    {
        var root = new Screen();
        var context = new RenderContext(new UiContext(root));
        var buffer = new ScreenBuffer(width, 1);
        var canvas = new Canvas(buffer, new(0, 0, width, 1), context, frameNumber: 1);
        spinner.GetScribe().Draw(canvas);
        return buffer;
    }
}
