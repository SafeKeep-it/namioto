using System.Diagnostics;
using Thoth.Rendering;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public sealed class Spinner : IWidget, IWidgetWithLayout
{
    internal readonly record struct spinner_frame(string Text);

    static readonly spinner_frame[] AsciiFrames =
    [
        new("|"),
        new("/"),
        new("-"),
        new("\\")
    ];

    static readonly spinner_frame[] AsciiDotsFrames =
    [
        new("."),
        new("o"),
        new("O"),
        new("@"),
        new("*"),
        new("o")
    ];

    static readonly spinner_frame[] BrailleFrames =
    [
        new("⠋"),
        new("⠙"),
        new("⠹"),
        new("⠸"),
        new("⠼"),
        new("⠴"),
        new("⠦"),
        new("⠧"),
        new("⠇"),
        new("⠏")
    ];

    readonly SpinnerScribe _scribe;
    int _frameIndex;
    long _lastAdvanceTicks;
    bool _started;

    public Spinner()
    {
        _scribe = new(this);
    }

    public IWidget Parent { get; set; } = SentinelWidget.Instance;

    public SpinnerDial Dial { get; set; } = SpinnerDial.Braille;

    public int Speed { get; set; } = 12;

    public bool IsAnimationActive { get; set; } = true;

    public Color? ForegroundColor { get; set; }

    public Color? BackgroundColor { get; set; }

    public int LaneWidth { get; set; } = 11;

    public int TrailRadius { get; set; } = 2;

    public IWidgetRenderer GetRenderer() => _scribe;

    public IWidgetScribe GetScribe() => _scribe;

    public ILayoutCreator GetLayoutCreator() => new SpinnerLayout();

    public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
    {
    }

    public void Accept<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IVisitor, allows ref struct
    {
    }

    public bool UpdateAnimation(long nowTicks)
    {
        if (!IsAnimationActive) return false;
        var frameCount = ResolveFrameCount(this);
        if (frameCount < 2) return false;

        if (!_started)
        {
            _lastAdvanceTicks = nowTicks;
            _started = true;
            return false;
        }

        var fps = Math.Clamp(Speed, 6, 12);
        var interval = Math.Max(1L, Stopwatch.Frequency / fps);
        var elapsed = nowTicks - _lastAdvanceTicks;
        if (elapsed < interval) return false;

        var steps = (int)(elapsed / interval);
        _lastAdvanceTicks += steps * interval;
        var next = (_frameIndex + steps) % frameCount;
        if (next == _frameIndex) return false;

        _frameIndex = next;
        return true;
    }

    internal string CurrentFrameText
    {
        get
        {
            if (Dial == SpinnerDial.Kit)
                return " ";

            var frames = ResolveFrames(Dial);
            if (frames.Length == 0) return " ";
            return frames[_frameIndex % frames.Length].Text;
        }
    }

    internal int CurrentFrameIndex => _frameIndex;

    internal static spinner_frame[] ResolveFrames(SpinnerDial dial)
    {
        return dial switch
        {
            SpinnerDial.Ascii => AsciiFrames,
            SpinnerDial.AsciiDots => AsciiDotsFrames,
            SpinnerDial.Braille => BrailleFrames,
            _ => AsciiFrames
        };
    }

    static int ResolveFrameCount(Spinner owner)
    {
        if (owner.Dial != SpinnerDial.Kit)
            return ResolveFrames(owner.Dial).Length;

        var lane = Math.Max(1, owner.LaneWidth);
        if (lane <= 1) return 1;
        return (lane - 1) * 2;
    }

}
