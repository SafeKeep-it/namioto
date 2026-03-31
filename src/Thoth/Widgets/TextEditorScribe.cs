using System.Globalization;
using System.Text;
using Thoth.Rendering;
using Thoth.Rendering.Layout;
using Thoth.Rendering.Text;

namespace Thoth.Widgets;

public sealed class TextEditorScribe : IWidgetRenderer, IWidgetScribe
{
    readonly TextEditor _widget;
    bool _isFlowValid;
    int _flowWidth;
    int _flowHeight;
    string _flowText = string.Empty;
    List<FlowLine> _flowLines = [];
    List<TextRun> _flowRuns = new(1);
    IWidthProvider _widthProvider = WidthProviders.Unicode();
    RenderContext? _cachedContext;
    int _defaultStyleIndex;
    bool _hasDefaultStyle;
    Style _cachedDefaultStyle;

    public TextEditorScribe(TextEditor widget)
    {
        _widget = widget;
    }

    public Size Measure(SizeConstraint constraint)
    {
        EnsureFlow(_widget, constraint.MaxWidth);
        return new(constraint.MaxWidth, Math.Max(Math.Max(1, _widget.MinHeight), _flowHeight));
    }

    public void Arrange(Rect rect)
    {
        EnsureFlow(_widget, rect.Width);
    }

    public void Draw(Canvas canvas)
    {
        EnsureCacheContext(canvas);
        var text = _widget.Text;
        var styleIndex = ResolveDefaultStyleIndex(_widget.Style, canvas);
        EnsureFlow(_widget, canvas.Width + canvas.OffsetX);

        for (var y = 0; y < _flowLines.Count; y++)
            DrawFlowLine(canvas, _flowLines[y], y, styleIndex);

        if (_widget.HasSelection)
        {
            var (start, end) = _widget.SelectionRange!.Value;
            var selectedStyle = _widget.Style with
            {
                Attributes = _widget.Style.Attributes | TextAttributes.Reverse
            };
            var maxWidth = canvas.Width + canvas.OffsetX;
            var enumerator = StringInfo.GetTextElementEnumerator(text);

            while (enumerator.MoveNext())
            {
                var index = enumerator.ElementIndex;
                if (index < start || index >= end) continue;

                var element = enumerator.GetTextElement();
                if (element is "\n" or "\r") continue;

                (var x, var y) = _widget.GetVisualPosition(text, index, maxWidth);
                if (element.Length == 1)
                    canvas.PutGlyph(x, y, (Rune)element[0], selectedStyle);
                else
                    canvas.PutGlyph(x, y, element, selectedStyle);
            }
        }

        if (_widget.CaretIndex <= text.Length)
        {
            (var visualX, var visualY) =
                _widget.GetVisualPosition(text, _widget.CaretIndex, canvas.Width + canvas.OffsetX);
            var cursorChar = _widget.CaretIndex < text.Length
                ? GetTextElementAt(text, _widget.CaretIndex)
                : " ";

            if (cursorChar.Length == 1)
                canvas.PutGlyph(visualX, visualY, (Rune)cursorChar[0],
                                _widget.Style with { Attributes = _widget.Style.Attributes | TextAttributes.Reverse });
            else
                canvas.PutGlyph(visualX, visualY, cursorChar,
                                _widget.Style with { Attributes = _widget.Style.Attributes | TextAttributes.Reverse });
        }
    }

    void EnsureFlow(TextEditor widget, int maxWidth)
    {
        maxWidth = Math.Max(1, maxWidth);
        var text = widget.Text;

        if (_isFlowValid && _flowWidth == maxWidth && string.Equals(_flowText, text, StringComparison.Ordinal))
            return;

        _flowRuns.Clear();
        _flowRuns.Add(new(text));
        var flow = TextFlowLayout.Build(_flowRuns, maxWidth, TextOverflow.Wrap, _widthProvider);

        _flowLines.Clear();
        for (var i = 0; i < flow.Lines.Count; i++)
            _flowLines.Add(flow.Lines[i]);

        _flowHeight = flow.Height;
        _flowWidth = maxWidth;
        _flowText = text;
        _isFlowValid = true;
    }

    static void DrawFlowLine(Canvas canvas, FlowLine line, int y, int defaultStyleIndex)
    {
        var x = 0;
        for (var i = 0; i < line.Segments.Count; i++)
        {
            var segment = line.Segments[i];
            var styleIndex = segment.StyleId?.Value ?? defaultStyleIndex;
            for (var j = 0; j < segment.Cells.Count; j++)
            {
                var cell = segment.Cells[j];
                if (cell.Width <= 0) continue;
                if (cell.Text.Length == 1 && cell.Text[0] == '\t')
                {
                    x += cell.Width;
                    continue;
                }

                canvas.PutPreparedGlyph(x, y, cell.Text, styleIndex, cell.Width);
                x += cell.Width;
            }
        }
    }

    void EnsureCacheContext(Canvas canvas)
    {
        if (ReferenceEquals(_cachedContext, canvas.Context)) return;
        _cachedContext = canvas.Context;
        _hasDefaultStyle = false;
    }

    int ResolveDefaultStyleIndex(Style style, Canvas canvas)
    {
        if (!_hasDefaultStyle || _cachedDefaultStyle != style)
        {
            _cachedDefaultStyle = style;
            _defaultStyleIndex = canvas.Context.Styles.Intern(style);
            _hasDefaultStyle = true;
        }

        return _defaultStyleIndex;
    }

    static string GetTextElementAt(string text, int index)
    {
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            if (enumerator.ElementIndex == index) return enumerator.GetTextElement();
        }

        return text[index].ToString();
    }
}
