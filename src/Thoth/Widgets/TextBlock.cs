using System.Diagnostics;
using Thoth.Rendering;
using Thoth.Rendering.Text;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public class TextBlock : IWidget, IWidgetWithLayout
{
    readonly TextBlockScribe _scribe;
    readonly List<TextRun> _runs = [];
    Align _align = Align.Left;
    TextOverflow _overflow = TextOverflow.Wrap;
    int _marqueeOffset;
    long _lastMarqueeTick;
    bool _marqueeStarted;
    int _marqueeCycleWidth = 1;
    int _contentVersion;
    List<TextTokenizer.TextRun>? _arrangedTokenRuns;
    TextLayout? _arrangedLayout;
    public WidthSizeMode WidthSizeMode { get; set; } = WidthSizeMode.Fill;
    public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;
    public int MarqueeSpeed { get; set; } = 2;

    public Align Align
    {
        get => _align;
        set
        {
            if (_align == value) return;
            _align = value;
            _scribe.ClearMeasuredCache();
        }
    }

    public Color? ForegroundColor { get; set; }
    public Color? BackgroundColor { get; set; }

    public TextOverflow Overflow
    {
        get => _overflow;
        set
        {
            if (_overflow == value) return;
            _overflow = value;
            _contentVersion++;
            ResetMarquee();
            _scribe.ClearMeasuredCache();
        }
    }

    public string Text
    {
        set => SetContent([new(value)]);
    }

    public IWidget Parent { get; set; } = SentinelWidget.Instance;

    public bool IsAnimationActive => Overflow == TextOverflow.Marquee;

    public TextBlock()
    {
        _scribe = new(this);
    }

    public IWidgetRenderer GetRenderer() => _scribe;

    public IWidgetScribe GetScribe() => _scribe;

    public ILayoutCreator GetLayoutCreator() => new TextBlockLayout();

    internal IReadOnlyList<TextRun> Runs => _runs;
    internal int ContentVersion => _contentVersion;

    public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
    {
    }

    public void Accept<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IVisitor, allows ref struct
    {
    }

    public void SetContent(ReadOnlySpan<TextRun> runs)
    {
        _runs.Clear();
        foreach (var run in runs) _runs.Add(run);
        _contentVersion++;
        ResetMarquee();
        _scribe.ClearMeasuredCache();
    }

    public bool UpdateAnimation(long nowTicks)
    {
        if (Overflow != TextOverflow.Marquee) return false;

        if (!_marqueeStarted)
        {
            _lastMarqueeTick = nowTicks;
            _marqueeStarted = true;
            return false;
        }

        var cps = Math.Max(1, MarqueeSpeed);
        var interval = Math.Max(1L, Stopwatch.Frequency / cps);
        var elapsed = nowTicks - _lastMarqueeTick;
        if (elapsed < interval) return false;

        var steps = (int)(elapsed / interval);
        _lastMarqueeTick += steps * interval;
        var cycle = Math.Max(1, _marqueeCycleWidth);
        var next = (_marqueeOffset + steps) % cycle;
        if (next == _marqueeOffset) return false;

        _marqueeOffset = next;
        return true;
    }

    internal int MarqueeOffset => _marqueeOffset;

    internal void SetArrangedLayout(List<TextTokenizer.TextRun> runs, TextLayout layout)
    {
        _arrangedTokenRuns = runs;
        _arrangedLayout = layout;
    }

    internal List<TextTokenizer.TextRun>? ArrangedTokenRuns => _arrangedTokenRuns;
    internal TextLayout? ArrangedLayout => _arrangedLayout;

    internal void SetMarqueeCycleWidth(int width)
    {
        _marqueeCycleWidth = Math.Max(1, width);
        if (_marqueeOffset >= _marqueeCycleWidth)
            _marqueeOffset %= _marqueeCycleWidth;
    }

    void ResetMarquee()
    {
        _marqueeOffset = 0;
        _marqueeStarted = false;
        _marqueeCycleWidth = 1;
    }
}
