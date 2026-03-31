using System.Text;
using Thoth.Rendering;
using Thoth.Rendering.Layout;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public sealed class ProgressBar : IWidget, IWidgetWithLayout
{
    readonly ProgressBarScribe _scribe;
    Rune _fillGlyph;
    Rune _trackGlyph;
    bool _hasFillGlyphOverride;
    bool _hasTrackGlyphOverride;
    ProgressBarStyle _style = ProgressBarStyle.Solid;

    public ProgressBar()
    {
        _scribe = new(this);
    }

    public IWidget Parent { get; set; } = SentinelWidget.Instance;

    public int Width { get; set; } = 12;

    public double Progress { get; set; }

    public Color? FillColor { get; set; }

    public Color? TrackColor { get; set; }

    public ProgressBarStyle Style
    {
        get => _style;
        set => _style = value;
    }

    public Rune FillGlyph
    {
        get => _hasFillGlyphOverride ? _fillGlyph : default_fill_glyph(_style);
        set
        {
            _fillGlyph = value;
            _hasFillGlyphOverride = true;
        }
    }

    public Rune TrackGlyph
    {
        get => _hasTrackGlyphOverride ? _trackGlyph : default_track_glyph(_style);
        set
        {
            _trackGlyph = value;
            _hasTrackGlyphOverride = true;
        }
    }

    public IWidgetRenderer GetRenderer() => _scribe;

    public IWidgetScribe GetScribe() => _scribe;

    public ILayoutCreator GetLayoutCreator() => new ProgressBarLayout();

    public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
    {
    }

    public void Accept<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IVisitor, allows ref struct
    {
    }

    static Rune default_fill_glyph(ProgressBarStyle style)
    {
        return style switch
        {
            ProgressBarStyle.Pulse => (Rune)'◉',
            _ => (Rune)'█'
        };
    }

    static Rune default_track_glyph(ProgressBarStyle style)
    {
        return style switch
        {
            ProgressBarStyle.Pulse => (Rune)'○',
            _ => (Rune)'░'
        };
    }
}
