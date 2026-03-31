using System.Runtime.InteropServices;
using System.Text;
using Thoth.Rendering;
using Thoth.Rendering.Layout;
using Thoth.Rendering.Text;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public class TextBlockLayout : ILayoutCreator
{
    public WidgetSizeRequest Measure(in IWidgetWithLayout widget,
                                     in SizeConstraint constraint,
                                     ReadOnlySpan<WidgetSizeRequest> desires)
    {
        var textBlock = widget as TextBlock
            ?? throw new InvalidOperationException($"{nameof(TextBlockLayout)} requires {nameof(TextBlock)}.");

        var (_, layout) = BuildTokenLayout(textBlock, constraint.MaxWidth);
        var lines = layout.Lines;

        var width = 0;
        for (var i = 0; i < lines.Count; i++)
            width = Math.Max(width, lines[i].EstimatedWidth);

        var size = new Size(width, lines.Count);
        return new(widget, this, size);
    }

    public void Arrange(in IWidgetWithLayout widget, in WidgetSize actual, ReadOnlySpan<WidgetSizeRequest> childDesires, Span<WidgetSize> children)
    {
        _ = childDesires;
        var textBlock = widget as TextBlock
            ?? throw new InvalidOperationException($"{nameof(TextBlockLayout)} requires {nameof(TextBlock)}.");

        BuildTokenLayout(textBlock, actual.Rect.Width);
    }

    public void Draw(in IWidgetWithLayout widget, in Canvas canvas)
    {
        var textBlock = widget as TextBlock
            ?? throw new InvalidOperationException($"{nameof(TextBlockLayout)} requires {nameof(TextBlock)}.");

        if (textBlock.BackgroundColor is { } bg)
            canvas.Fill(0, 0, canvas.Width, canvas.Height, new(' '), new(Background: bg));

        var tokenRuns = textBlock.ArrangedTokenRuns;
        var layout = textBlock.ArrangedLayout;
        if (tokenRuns == null || layout == null) return;

        if (textBlock.Overflow == TextOverflow.Marquee)
        {
            DrawTokenizedMarquee(textBlock, canvas, tokenRuns, layout);
            return;
        }

        DrawTokenizedLines(textBlock, canvas, tokenRuns, layout);
    }

    (List<TextTokenizer.TextRun> runs, TextLayout layout) BuildTokenLayout(TextBlock widget, int maxWidth)
    {
        var tokenRuns = new List<TextTokenizer.TextRun>(widget.Runs.Count);
        for (var i = 0; i < widget.Runs.Count; i++)
        {
            var utf8 = Encoding.UTF8.GetBytes(widget.Runs[i].Text);
            tokenRuns.Add(new(utf8));
        }

        var tokenizer = new TextTokenizer();
        var tokenDelta = tokenizer.Tokenize(tokenRuns);
        var layout = new TextLayout();
        layout.Initialize(tokenDelta);
        layout.Reflow(maxWidth, widget.Overflow);
        return (tokenRuns, layout);
    }

    void DrawTokenizedLines(TextBlock widget,
                            Canvas canvas,
                            List<TextTokenizer.TextRun> tokenRuns,
                            TextLayout layout)
    {
        var styleIndices = ResolveStyleIndices(widget, canvas);
        var tokens = CollectionsMarshal.AsSpan(layout.Tokens);
        var runs = CollectionsMarshal.AsSpan(tokenRuns);
        var lines = layout.Lines;
        var maxLines = Math.Min(lines.Count, canvas.Height);

        for (var lineIndex = 0; lineIndex < maxLines; lineIndex++)
        {
            var line = lines[lineIndex];
            var x = widget.Align == Align.Right ? canvas.Width - line.EstimatedWidth : 0;
            canvas.DrawTokenLine(x, lineIndex, tokens, line.TokenStart, line.TokenCount, runs, styleIndices);

            var isLastVisible = lineIndex == maxLines - 1;
            var hasOverflow = lineIndex < lines.Count - 1 || line.EstimatedWidth > canvas.Width;
            if (isLastVisible && hasOverflow && widget.Overflow == TextOverflow.Ellipsis && canvas.Width >= 1)
            {
                var lastRunIndex = line.TokenCount > 0
                    ? tokens[line.TokenStart + line.TokenCount - 1].RunIndex
                    : 0;
                var ellipsisStyleIndex = lastRunIndex < styleIndices.Length
                    ? styleIndices[lastRunIndex]
                    : 0;
                var ellipsis = canvas.PrepareRune(new('\u2026'));
                canvas.PutPreparedGlyph(canvas.Width - 1, lineIndex, ellipsis, ellipsisStyleIndex);
            }
        }
    }

    void DrawTokenizedMarquee(TextBlock widget,
                              Canvas canvas,
                              List<TextTokenizer.TextRun> tokenRuns,
                              TextLayout layout)
    {
        if (canvas.Height <= 0 || canvas.Width <= 0) return;

        var tokens = CollectionsMarshal.AsSpan(layout.Tokens);
        var tokenCount = tokens.Length;
        if (tokenCount == 0) return;

        var totalWidth = 0;
        for (var i = 0; i < tokenCount; i++)
            totalWidth += tokens[i].EstimatedWidth;

        var styleIndices = ResolveStyleIndices(widget, canvas);
        var runs = CollectionsMarshal.AsSpan(tokenRuns);

        if (totalWidth <= canvas.Width)
        {
            canvas.DrawTokenLine(0, 0, tokens, 0, tokenCount, runs, styleIndices);
            return;
        }

        var gapWidth = 3;
        var cycleWidth = totalWidth + gapWidth;
        var offset = widget.MarqueeOffset % cycleWidth;

        canvas.DrawTokenLine(-offset, 0, tokens, 0, tokenCount, runs, styleIndices);
        canvas.DrawTokenLine(cycleWidth - offset, 0, tokens, 0, tokenCount, runs, styleIndices);
    }

    int[] ResolveStyleIndices(TextBlock widget, Canvas canvas)
    {
        var runCount = widget.Runs.Count;
        if (runCount == 0) return [];

        var styleIndices = new int[runCount];
        for (var i = 0; i < runCount; i++)
            styleIndices[i] = ResolveStyleIndex(widget, canvas, widget.Runs[i].StyleId, widget.Runs[i].LinkId);

        return styleIndices;
    }

    int ResolveStyleIndex(TextBlock widget, Canvas canvas, StyleId? styleId, LinkId? linkId)
    {
        var style = styleId.HasValue
            ? canvas.Context.Styles.Get(styleId.Value.Value)
            : new Style();

        if (style.Foreground == null && widget.ForegroundColor != null)
            style = style with { Foreground = widget.ForegroundColor };

        if (style.Background == null && widget.BackgroundColor != null)
            style = style with { Background = widget.BackgroundColor };

        if (linkId.HasValue)
            style = style with { Hyperlink = canvas.Context.Links.Get(linkId.Value.Value) };

        return canvas.Context.Styles.Intern(style);
    }
}
