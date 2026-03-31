using System.Diagnostics;
using Shouldly;
using Thoth.Rendering;
using Thoth.Widgets;

namespace Thoth.Tests.widgets.spinner;

public class advances_once_per_second_by_default
{
    [Fact]
    public void does_not_advance_before_one_twelfth_second_by_default()
    {
        var spinner = new Spinner();
        var start = Stopwatch.GetTimestamp();
        var interval = Math.Max(1L, Stopwatch.Frequency / 12);

        spinner.UpdateAnimation(start).ShouldBeFalse();
        CurrentGlyph(spinner).ShouldBe('⠋');

        spinner.UpdateAnimation(start + interval - 1).ShouldBeFalse();
        CurrentGlyph(spinner).ShouldBe('⠋');
    }

    [Fact]
    public void advances_after_one_twelfth_second_by_default()
    {
        var spinner = new Spinner();
        var start = Stopwatch.GetTimestamp();
        var interval = Math.Max(1L, Stopwatch.Frequency / 12);

        spinner.UpdateAnimation(start).ShouldBeFalse();
        spinner.UpdateAnimation(start + interval).ShouldBeTrue();

        CurrentGlyph(spinner).ShouldBe('⠙');
    }

    [Fact]
    public void braille_dial_is_default_and_other_dials_can_be_selected()
    {
        var spinner = new Spinner();
        spinner.Dial.ShouldBe(SpinnerDial.Braille);
        CurrentGlyph(spinner).ShouldBe('⠋');

        spinner.Dial = SpinnerDial.Ascii;
        CurrentGlyph(spinner).ShouldBe('|');

        spinner.Dial = SpinnerDial.AsciiDots;
        CurrentGlyph(spinner).ShouldBe('.');
    }

    [Fact]
    public void foreground_color_sets_foreground_color()
    {
        var spinner = new Spinner { ForegroundColor = new(12, 34, 56) };
        var (glyph, style) = RenderCell(spinner);

        glyph.ShouldBe('⠋');
        style.Foreground.ShouldBe(new Color(12, 34, 56));
    }

    [Fact]
    public void kit_is_multi_character_and_moves_with_gradient_tail()
    {
        var spinner = new Spinner
                      {
                          Dial = SpinnerDial.Kit,
                          ForegroundColor = new(255, 180, 60),
                          BackgroundColor = new(20, 20, 20)
                      };
        var start = Stopwatch.GetTimestamp();
        var interval = Math.Max(1L, Stopwatch.Frequency / 12);

        spinner.GetRenderer().Measure(new(40, 1)).Width.ShouldBeGreaterThan(1);

        var frame0 = RenderFrame(spinner, 11);
        frame0.Line.ShouldContain('■');
        var head0 = HeadPosition(frame0);

        spinner.UpdateAnimation(start).ShouldBeFalse();
        spinner.UpdateAnimation(start + interval).ShouldBeTrue();
        var frame1 = RenderFrame(spinner, 11);
        var head1 = HeadPosition(frame1);
        head1.ShouldBeGreaterThan(head0);

        spinner.UpdateAnimation(start + (interval * 2)).ShouldBeTrue();
        var frame2 = RenderFrame(spinner, 11);
        frame2.Line.ShouldContain('■');

        var trailIndex = Math.Max(0, head1 - 1);
        var headColor = frame1.Cells[head1].Style.Foreground;
        var trailColor = frame1.Cells[trailIndex].Style.Foreground;

        headColor.ShouldNotBeNull();
        trailColor.ShouldNotBeNull();
        Brightness(trailColor.Value).ShouldBeLessThan(Brightness(headColor.Value));
    }

    static char CurrentGlyph(Spinner spinner)
    {
        var (glyph, _) = RenderCell(spinner);
        return glyph;
    }

    static (char glyph, Style style) RenderCell(Spinner spinner)
    {
        var root = new Screen();
        var context = new RenderContext(new UiContext(root));
        var buffer = new ScreenBuffer(1, 1);
        var canvas = new Canvas(buffer, new(0, 0, 1, 1), context, frameNumber: 1);

        spinner.GetScribe().Draw(canvas);

        var cell = buffer.GetCell(0, 0);
        context.Styles.TryGet(cell.StyleIndex, out var style).ShouldBeTrue();
        return ((char)cell.GlyphId, style);
    }

    static RenderedFrame RenderFrame(Spinner spinner, int width)
    {
        var root = new Screen();
        var context = new RenderContext(new UiContext(root));
        var buffer = new ScreenBuffer(width, 1);
        var canvas = new Canvas(buffer, new(0, 0, width, 1), context, frameNumber: 1);

        spinner.GetScribe().Draw(canvas);

        var chars = new char[width];
        var cells = new RenderedCell[width];
        for (var i = 0; i < width; i++)
        {
            var cell = buffer.GetCell(i, 0);
            chars[i] = (char)cell.GlyphId;
            context.Styles.TryGet(cell.StyleIndex, out var style).ShouldBeTrue();
            cells[i] = new(chars[i], style);
        }

        return new(new(chars), cells);
    }

    static int HeadPosition(RenderedFrame frame)
    {
        var bestIndex = -1;
        var bestBrightness = double.MinValue;

        for (var i = 0; i < frame.Cells.Length; i++)
        {
            var color = frame.Cells[i].Style.Foreground;
            if (color is null) continue;

            var brightness = Brightness(color.Value);
            if (brightness <= bestBrightness) continue;
            bestBrightness = brightness;
            bestIndex = i;
        }

        bestIndex.ShouldBeGreaterThanOrEqualTo(0);
        return bestIndex;
    }

    static double Brightness(Color color) => color.R + color.G + color.B;

    readonly record struct RenderedCell(char Glyph, Style Style);
    readonly record struct RenderedFrame(string Line, RenderedCell[] Cells);
}
