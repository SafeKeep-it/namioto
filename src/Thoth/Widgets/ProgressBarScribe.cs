using System;
using System.Text;
using Thoth.Rendering;
using Thoth.Rendering.Layout;

namespace Thoth.Widgets;

public sealed class ProgressBarScribe : IWidgetRenderer, IWidgetScribe
{
    readonly ProgressBar _widget;
    
    // Layout state
    
    // Cache state
    RenderContext? _cachedContext;
    
    // Fill style cache
    Color? _fillColor;
    int _fillStyleIndex;
    bool _hasFillStyle;
    
    // Track style cache
    Color? _trackColor;
    int _trackStyleIndex;
    bool _hasTrackStyle;
    
    // Fill glyph cache
    Rune _fillRune;
    Canvas.PreparedGlyph _fillPreparedGlyph;
    bool _hasFillGlyph;
    
    // Track glyph cache
    Rune _trackRune;
    Canvas.PreparedGlyph _trackPreparedGlyph;
    bool _hasTrackGlyph;

    public ProgressBarScribe(ProgressBar widget)
    {
        _widget = widget;
    }

    public Size Measure(SizeConstraint constraint)
    {
        var width = Math.Min(Math.Max(0, _widget.Width), constraint.MaxWidth);
        return new(width, Math.Min(1, constraint.MaxHeight));
    }

    public void Arrange(Rect rect)
    {
    }

    public void Draw(Canvas canvas)
    {
        EnsureCacheContext(canvas);
        if (canvas.Width <= 0 || canvas.Height <= 0) return;

        var width = Math.Min(Math.Max(0, _widget.Width), canvas.Width);
        if (width <= 0) return;

        var clampedProgress = Math.Clamp(_widget.Progress, 0d, 1d);
        var fillCells = (int)Math.Round(clampedProgress * width, MidpointRounding.AwayFromZero);
        fillCells = Math.Clamp(fillCells, 0, width);

        var trackStyleIndex = GetTrackStyleIndex(_widget.TrackColor, canvas);
        var fillStyleIndex = GetFillStyleIndex(_widget.FillColor, canvas);
        var trackGlyph = GetTrackGlyph(_widget.TrackGlyph, canvas);
        var fillGlyph = GetFillGlyph(_widget.FillGlyph, canvas);

        canvas.FillPreparedGlyph(0, 0, width, 1, trackGlyph, trackStyleIndex);
        if (fillCells > 0)
            canvas.FillPreparedGlyph(0, 0, fillCells, 1, fillGlyph, fillStyleIndex);
    }

    void EnsureCacheContext(Canvas canvas)
    {
        if (ReferenceEquals(_cachedContext, canvas.Context)) return;
        _cachedContext = canvas.Context;
        _hasFillStyle = false;
        _hasTrackStyle = false;
        _hasFillGlyph = false;
        _hasTrackGlyph = false;
    }

    int GetFillStyleIndex(Color? fillColor, Canvas canvas)
    {
        if (!_hasFillStyle || _fillColor != fillColor)
        {
            _fillColor = fillColor;
            _fillStyleIndex = canvas.Context.Styles.Intern(new Style(fillColor, null));
            _hasFillStyle = true;
        }

        return _fillStyleIndex;
    }

    int GetTrackStyleIndex(Color? trackColor, Canvas canvas)
    {
        if (!_hasTrackStyle || _trackColor != trackColor)
        {
            _trackColor = trackColor;
            _trackStyleIndex = canvas.Context.Styles.Intern(new Style(trackColor, null));
            _hasTrackStyle = true;
        }

        return _trackStyleIndex;
    }

    Canvas.PreparedGlyph GetFillGlyph(Rune rune, Canvas canvas)
    {
        if (!_hasFillGlyph || _fillRune != rune)
        {
            _fillRune = rune;
            _fillPreparedGlyph = canvas.PrepareRune(rune);
            _hasFillGlyph = true;
        }

        return _fillPreparedGlyph;
    }

    Canvas.PreparedGlyph GetTrackGlyph(Rune rune, Canvas canvas)
    {
        if (!_hasTrackGlyph || _trackRune != rune)
        {
            _trackRune = rune;
            _trackPreparedGlyph = canvas.PrepareRune(rune);
            _hasTrackGlyph = true;
        }

        return _trackPreparedGlyph;
    }
}
