using System.Text;
using Thoth.Rendering;
using Thoth.Rendering.Layout;

namespace Thoth.Widgets;

public sealed class SpinnerScribe : IWidgetRenderer, IWidgetScribe
{
    readonly Spinner _widget;
    bool _hasPreviousKitTrail;
    int _previousKitCenter;
    int _previousKitDirection;
    int _previousKitRadius;
    int _previousKitLane;

    public SpinnerScribe(Spinner widget)
    {
        _widget = widget;
    }

    public Size Measure(SizeConstraint constraint)
    {
        var frameWidth = _widget.Dial == SpinnerDial.Kit
            ? Math.Max(1, _widget.LaneWidth)
            : Spinner.ResolveFrames(_widget.Dial).Max(static frame => frame.Text.Length);
        var width = Math.Min(frameWidth, constraint.MaxWidth);
        var height = Math.Min(1, constraint.MaxHeight);
        return new(width, height);
    }

    public void Arrange(Rect rect)
    {
    }

    public void Draw(Canvas canvas)
    {
        if (canvas.Width <= 0 || canvas.Height <= 0) return;

        if (_widget.Dial == SpinnerDial.Kit)
        {
            DrawKit(canvas);
            return;
        }

        ResetKitTrail();

        var style = new Style(_widget.ForegroundColor, _widget.BackgroundColor);
        canvas.DrawString(0, 0, _widget.CurrentFrameText, style);
    }

    void DrawKit(Canvas canvas)
    {
        var lane = Math.Min(canvas.Width, Math.Max(1, _widget.LaneWidth));
        var styleBackground = new Style(null, _widget.BackgroundColor);

        if (lane <= 0) return;

        var radius = Math.Max(1, _widget.TrailRadius);
        var period = Math.Max(1, (lane - 1) * 2);
        var step = _widget.CurrentFrameIndex % period;
        var center = step < lane ? step : period - step;
        var direction = step < lane - 1 ? 1 : -1;

        if (_hasPreviousKitTrail
            && _previousKitLane == lane
            && _previousKitRadius == radius)
        {
            ClearTrailCells(canvas, styleBackground, _previousKitCenter, _previousKitDirection, _previousKitRadius, lane);
        }
        else
        {
            canvas.Fill(0, 0, lane, 1, new(' '), styleBackground);
        }

        var foreground = _widget.ForegroundColor ?? global::Thoth.Rendering.Color.White;
        var background = _widget.BackgroundColor ?? global::Thoth.Rendering.Color.Black;

        var headStyle = new Style(foreground, _widget.BackgroundColor);
        canvas.PutGlyph(center, 0, (Rune)'■', headStyle);

        for (var i = 1; i <= radius; i++)
        {
            var x = center - (direction * i);
            if (x < 0 || x >= lane) continue;

            var intensity = 1.0 - (i / (double)(radius + 1));
            if (intensity <= 0.0) continue;

            var color = Blend(background, foreground, intensity);
            var trailStyle = new Style(color, _widget.BackgroundColor);
            canvas.PutGlyph(x, 0, (Rune)'■', trailStyle);
        }

        _hasPreviousKitTrail = true;
        _previousKitCenter = center;
        _previousKitDirection = direction;
        _previousKitRadius = radius;
        _previousKitLane = lane;
    }

    static void ClearTrailCells(Canvas canvas, Style styleBackground, int center, int direction, int radius, int lane)
    {
        for (var i = 0; i <= radius; i++)
        {
            var x = i == 0 ? center : center - (direction * i);
            if (x < 0 || x >= lane) continue;
            canvas.PutGlyph(x, 0, (Rune)' ', styleBackground);
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

    void ResetKitTrail()
    {
        _hasPreviousKitTrail = false;
        _previousKitCenter = 0;
        _previousKitDirection = 0;
        _previousKitRadius = 0;
        _previousKitLane = 0;
    }
}
