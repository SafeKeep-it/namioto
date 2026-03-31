using System.Globalization;
using System.Text;
using Thoth.Rendering;
using Thoth.Rendering.Layout;
using Thoth.Rendering.Text;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public class TextEditorLayout : ILayoutCreator
{
    public WidgetSizeRequest Measure(in IWidgetWithLayout widget,
                                     in SizeConstraint constraint,
                                     ReadOnlySpan<WidgetSizeRequest> desires)
    {
        var textEditor = widget as TextEditor
            ?? throw new InvalidOperationException($"{nameof(TextEditorLayout)} requires {nameof(TextEditor)}.");

        _ = desires;
        var flow = BuildFlow(textEditor, constraint.MaxWidth);
        var height = Math.Max(Math.Max(1, textEditor.MinHeight), flow.Height);
        return new(textEditor, this, new Size(constraint.MaxWidth, height));
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
        var textEditor = widget as TextEditor
            ?? throw new InvalidOperationException($"{nameof(TextEditorLayout)} requires {nameof(TextEditor)}.");

        var text = textEditor.Text;
        var styleIndex = canvas.Context.Styles.Intern(textEditor.Style);
        var maxWidth = canvas.Width + canvas.OffsetX;
        var flow = BuildFlow(textEditor, maxWidth);

        for (var y = 0; y < flow.Lines.Count; y++)
            DrawFlowLine(canvas, flow.Lines[y], y, styleIndex);

        if (textEditor.HasSelection)
        {
            var (start, end) = textEditor.SelectionRange!.Value;
            var selectedStyle = textEditor.Style with
            {
                Attributes = textEditor.Style.Attributes | TextAttributes.Reverse
            };
            var enumerator = StringInfo.GetTextElementEnumerator(text);

            while (enumerator.MoveNext())
            {
                var index = enumerator.ElementIndex;
                if (index < start || index >= end) continue;

                var element = enumerator.GetTextElement();
                if (element is "\n" or "\r") continue;

                (var x, var y) = textEditor.GetVisualPosition(text, index, maxWidth);
                if (element.Length == 1)
                    canvas.PutGlyph(x, y, (Rune)element[0], selectedStyle);
                else
                    canvas.PutGlyph(x, y, element, selectedStyle);
            }
        }

        if (textEditor.CaretIndex <= text.Length)
        {
            (var visualX, var visualY) = textEditor.GetVisualPosition(text, textEditor.CaretIndex, maxWidth);
            var cursorChar = textEditor.CaretIndex < text.Length
                ? GetTextElementAt(text, textEditor.CaretIndex)
                : " ";
            var cursorStyle = textEditor.Style with
            {
                Attributes = textEditor.Style.Attributes | TextAttributes.Reverse
            };

            if (cursorChar.Length == 1)
                canvas.PutGlyph(visualX, visualY, (Rune)cursorChar[0], cursorStyle);
            else
                canvas.PutGlyph(visualX, visualY, cursorChar, cursorStyle);
        }
    }

    FlowResult BuildFlow(TextEditor widget, int maxWidth)
    {
        List<TextRun> flowRuns = [new(widget.Text)];
        return TextFlowLayout.Build(flowRuns, maxWidth, TextOverflow.Wrap, WidthProviders.Unicode());
    }

    void DrawFlowLine(Canvas canvas, FlowLine line, int y, int defaultStyleIndex)
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

    string GetTextElementAt(string text, int index)
    {
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            if (enumerator.ElementIndex == index) return enumerator.GetTextElement();
        }

        return text[index].ToString();
    }
}
