using System.Text;
using Shouldly;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.progress_bar_rendering;

public class animation_snapshot_steps
{
    [Fact]
    public void writes_svg_regression_strip_for_progress_steps()
    {
        var bar = new ProgressBar
                  {
                       Width = 16,
                       Style = ProgressBarStyle.Solid,
                       FillColor = new(80, 200, 120),
                       TrackColor = new(40, 60, 45)
                  };

        var steps = new List<terminal_snapshot_assertions.AnimationStep>(11);
        for (var i = 0; i <= 10; i++)
        {
            bar.Progress = i / 10.0;
            var snapshot = JsonTerminal.Capture(Render(bar, bar.Width));
            steps.Add(new($"step {i:00} progress={bar.Progress:0.0}", snapshot));
        }

        terminal_snapshot_assertions.WriteAnimationStepsSvg(
            "progress_bar.steps.animation.svg",
            steps,
            new
            {
                control = "ProgressBar",
                style = bar.Style.ToString(),
                width = bar.Width,
                fillGlyph = bar.FillGlyph.ToString(),
                trackGlyph = bar.TrackGlyph.ToString()
            });

        steps.Count.ShouldBe(11);
    }

    static ScreenBuffer Render(ProgressBar bar, int width)
    {
        var root = new Screen();
        var context = new RenderContext(new UiContext(root));
        var buffer = new ScreenBuffer(width, 1);
        var canvas = new Canvas(buffer, new(0, 0, width, 1), context, frameNumber: 1);
        bar.GetScribe().Draw(canvas);
        return buffer;
    }
}
