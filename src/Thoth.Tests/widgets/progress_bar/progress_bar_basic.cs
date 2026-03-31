using System.Text;
using Shouldly;
using Thoth.Rendering;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.progress_bar_rendering;

public class progress_bar_basic
{
    [Fact]
    public void respects_width_property_in_measure()
    {
        var bar = new ProgressBar { Width = 14 };

        var size = bar.GetRenderer().Measure(new(100, 10));

        size.Width.ShouldBe(14);
        size.Height.ShouldBe(1);
    }

    [Fact]
    public void renders_filled_and_track_cells_from_progress()
    {
        var bar = new ProgressBar
                  {
                      Width = 10,
                      Progress = 0.5,
                      FillGlyph = (Rune)'#',
                      TrackGlyph = (Rune)'.',
                      FillColor = new(200, 60, 60),
                      TrackColor = new(70, 70, 70)
                  };

        var frame = Render(bar, width: 10);

        frame.Line.ShouldBe("#####.....");
    }

    [Fact]
    public void uses_solid_style_defaults_when_no_custom_glyphs_are_set()
    {
        var bar = new ProgressBar
                  {
                      Width = 8,
                      Progress = 0.5,
                      Style = ProgressBarStyle.Solid
                  };

        var frame = Render(bar, width: 8);

        frame.Line.ShouldBe("████░░░░");
    }

    [Fact]
    public void uses_pulse_style_defaults_when_selected()
    {
        var bar = new ProgressBar
                  {
                      Width = 8,
                      Progress = 0.5,
                      Style = ProgressBarStyle.Pulse
                  };

        var frame = Render(bar, width: 8);

        frame.Line.ShouldBe("◉◉◉◉○○○○");
    }

    [Fact]
    public void clamps_progress_to_valid_range()
    {
        var empty = new ProgressBar { Width = 8, Progress = -1, FillGlyph = (Rune)'#', TrackGlyph = (Rune)'.' };
        var full = new ProgressBar { Width = 8, Progress = 2, FillGlyph = (Rune)'#', TrackGlyph = (Rune)'.' };

        Render(empty, 8).Line.ShouldBe("........");
        Render(full, 8).Line.ShouldBe("########");
    }

    readonly record struct RenderedFrame(string Line);

    static RenderedFrame Render(ProgressBar bar, int width)
    {
        var root = new Screen();
        var context = new RenderContext(new UiContext(root));
        var buffer = new ScreenBuffer(width, 1);
        var canvas = new Canvas(buffer, new(0, 0, width, 1), context, frameNumber: 1);
        bar.GetScribe().Draw(canvas);

        var chars = new char[width];
        for (var i = 0; i < width; i++)
            chars[i] = (char)buffer.GetCell(i, 0).GlyphId;

        return new(new(chars));
    }
}
