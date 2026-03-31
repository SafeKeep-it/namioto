using System.Text;
using Thoth.Rendering;
using Thoth.Rendering.Layout;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public class SpinnerLayout : ILayoutCreator
{
    public WidgetSizeRequest Measure(in IWidgetWithLayout widget,
                                     in SizeConstraint constraint,
                                     ReadOnlySpan<WidgetSizeRequest> desires)
    {
        var spinner = widget as Spinner
            ?? throw new InvalidOperationException($"{nameof(SpinnerLayout)} requires {nameof(Spinner)}.");

        _ = desires;
        var frameWidth = spinner.Dial == SpinnerDial.Kit
            ? Math.Max(1, spinner.LaneWidth)
            : Spinner.ResolveFrames(spinner.Dial).Max(static frame => frame.Text.Length);
        var width = Math.Min(frameWidth, constraint.MaxWidth);
        var height = Math.Min(1, constraint.MaxHeight);
        var size = new Size(width, height);
        return new(spinner, this, size);
    }

    public void Arrange(in IWidgetWithLayout widget, in WidgetSize actual, ReadOnlySpan<WidgetSizeRequest> childDesires, Span<WidgetSize> children)
    {
        _ = widget;
        _ = actual;
        _ = childDesires;
        _ = children;
    }

    public void Draw(in IWidgetWithLayout widget, in Canvas canvas)
    {
        var spinner = widget as Spinner
            ?? throw new InvalidOperationException($"{nameof(SpinnerLayout)} requires {nameof(Spinner)}.");

        if (canvas.Width <= 0 || canvas.Height <= 0) return;

        if (spinner.Dial == SpinnerDial.Kit)
        {
            DrawKit(spinner, canvas);
            return;
        }

        var style = new Style(spinner.ForegroundColor, spinner.BackgroundColor);
        canvas.DrawString(0, 0, spinner.CurrentFrameText, style);
    }

    void DrawKit(Spinner spinner, Canvas canvas)
    {
        var lane = Math.Min(canvas.Width, Math.Max(1, spinner.LaneWidth));
        var styleBackground = new Style(null, spinner.BackgroundColor);

        if (lane <= 0) return;

        canvas.Fill(0, 0, lane, 1, new(' '), styleBackground);

        var radius = Math.Max(1, spinner.TrailRadius);
        var period = Math.Max(1, (lane - 1) * 2);
        var step = spinner.CurrentFrameIndex % period;
        var center = step < lane ? step : period - step;
        var direction = step < lane - 1 ? 1 : -1;

        var foreground = spinner.ForegroundColor ?? global::Thoth.Rendering.Color.White;
        var background = spinner.BackgroundColor ?? global::Thoth.Rendering.Color.Black;

        var headStyle = new Style(foreground, spinner.BackgroundColor);
        canvas.PutGlyph(center, 0, (Rune)'■', headStyle);

        for (var i = 1; i <= radius; i++)
        {
            var x = center - (direction * i);
            if (x < 0 || x >= lane) continue;

            var intensity = 1.0 - (i / (double)(radius + 1));
            if (intensity <= 0.0) continue;

            var color = Blend(background, foreground, intensity);
            var trailStyle = new Style(color, spinner.BackgroundColor);
            canvas.PutGlyph(x, 0, (Rune)'■', trailStyle);
        }
    }

    static global::Thoth.Rendering.Color Blend(global::Thoth.Rendering.Color from,
                                               global::Thoth.Rendering.Color to,
                                               double t)
    {
        t = Math.Clamp(t, 0d, 1d);
        return new(Lerp(from.R, to.R, t), Lerp(from.G, to.G, t), Lerp(from.B, to.B, t));
    }

    static byte Lerp(byte from, byte to, double t)
    {
        return (byte)Math.Round(from + ((to - from) * t));
    }
}
