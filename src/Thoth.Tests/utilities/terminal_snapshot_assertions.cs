using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Shouldly;
using Thoth.Rendering;
using Thoth.Widgets;

namespace Thoth.Tests.utilities;

public static class terminal_snapshot_assertions
{
    public sealed record AnimationStep(string Label, TerminalSnapshot Snapshot);

    public sealed record TrueColorInteractionFrame(
        TerminalSnapshot Snapshot,
        int DurationMs,
        int? MouseX = null,
        int? MouseY = null);

    public static void WriteInvalidationOverlaySvg(IWidget root,
                                                   int width,
                                                   int height,
                                                   string snapshotFileName,
                                                   IReadOnlyDictionary<IWidget, InvalidationKind>? invalidations,
                                                   [CallerFilePath] string callerFilePath = "")
    {
        WriteInvalidationOverlaySvg(root,
                                    width,
                                    height,
                                    snapshotFileName,
                                    null,
                                    invalidations,
                                    callerFilePath);
    }

    public static void WriteInvalidationOverlaySvg(IWidget root,
                                                   int width,
                                                   int height,
                                                   string snapshotFileName,
                                                   FrameLayoutState? layoutState = null,
                                                   IReadOnlyDictionary<IWidget, InvalidationKind>? invalidations = null,
                                                   [CallerFilePath] string callerFilePath = "")
    {
        var targetPath = ResolveSnapshotPath(callerFilePath, snapshotFileName);

        var svgPath = GetInvalidationSvgPath(targetPath);
        var parent = Path.GetDirectoryName(svgPath);
        if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);

        File.WriteAllText(svgPath,
                          RenderInvalidationOverlaySvg(root,
                                                       width,
                                                       height,
                                                       layoutState,
                                                       invalidations));
    }

    public static void WriteLayoutDebugSvg(this ScreenBuffer buffer,
                                           IWidget root,
                                           int width,
                                           int height,
                                           string snapshotFileName,
                                           FrameLayoutState? layoutState = null,
                                           int? cursorX = null,
                                           int? cursorY = null,
                                           [CallerFilePath] string callerFilePath = "")
    {
        var targetPath = ResolveSnapshotPath(callerFilePath, snapshotFileName);

        var svgPath = GetOutlinesSvgPath(targetPath);
        var parent = Path.GetDirectoryName(svgPath);
        if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);

        var snapshot = JsonTerminal.Capture(buffer);
        File.WriteAllText(svgPath,
                          RenderLayoutDebugSvg(snapshot,
                                               root,
                                               width,
                                               height,
                                               layoutState,
                                               cursorX,
                                               cursorY));
    }

    public static void WriteTerminalSnapshotSvg(this ScreenBuffer buffer,
                                                string snapshotFileName,
                                                int? cursorX = null,
                                                int? cursorY = null,
                                                [CallerFilePath] string callerFilePath = "")
    {
        var expectedPath = ResolveSnapshotPath(callerFilePath, snapshotFileName);

        var snapshot = JsonTerminal.Capture(buffer);
        var parent = Path.GetDirectoryName(expectedPath);
        if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
        File.WriteAllText(expectedPath, RenderSvg(snapshot, cursorX, cursorY));
        WriteActualSvg(expectedPath, snapshot, cursorX, cursorY);
    }

    public static void WriteAnimationStepsSvg(string snapshotFileName,
                                              IReadOnlyList<AnimationStep> steps,
                                              object? metadata = null,
                                              [CallerFilePath] string callerFilePath = "")
    {
        if (steps.Count == 0)
            throw new ArgumentException("At least one animation step is required.", nameof(steps));

        var expectedPath = ResolveSnapshotPath(callerFilePath, snapshotFileName);
        var parent = Path.GetDirectoryName(expectedPath);
        if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);

        File.WriteAllText(expectedPath, RenderAnimationStepsSvg(steps, metadata));
    }

    public static void WriteTrueColorAnimatedSvg(string snapshotFileName,
                                                 IReadOnlyList<TrueColorInteractionFrame> frames,
                                                 RenderContext context,
                                                 [CallerFilePath] string callerFilePath = "")
    {
        if (frames.Count == 0)
            throw new ArgumentException("At least one frame is required.", nameof(frames));

        var targetPath = ResolveSnapshotPath(callerFilePath, snapshotFileName);
        var parent = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);

        File.WriteAllText(targetPath, RenderTrueColorAnimatedSvg(frames, context));
    }

    public static void ShouldMatchTerminalSnapshot(this ScreenBuffer buffer,
                                                   string snapshotFileName,
                                                   [CallerFilePath] string callerFilePath = "")
    {
        var expectedPath = ResolveSnapshotPath(callerFilePath, snapshotFileName);

        buffer.ShouldMatchTerminalSnapshotAtPath(expectedPath);
    }

    static string ResolveSnapshotPath(string callerFilePath, string snapshotFileName)
    {
        var rawPath = snapshotFileName;

        if (!Path.IsPathRooted(rawPath))
        {
            var callerDirectory = Path.GetDirectoryName(callerFilePath) ??
                                  throw new InvalidOperationException(
                                      "Caller file path was not available.");
            rawPath = Path.GetFullPath(Path.Combine(callerDirectory, snapshotFileName));
        }

        if (Path.GetExtension(rawPath).Equals(".json", StringComparison.OrdinalIgnoreCase))
            return Path.ChangeExtension(rawPath, ".svg");

        return rawPath;
    }

    public static void ShouldMatchTerminalSnapshotAtPath(this ScreenBuffer buffer,
                                                         string expectedPath)
    {
        if (Path.GetExtension(expectedPath).Equals(".json", StringComparison.OrdinalIgnoreCase))
            expectedPath = Path.ChangeExtension(expectedPath, ".svg");

        var actualSnapshot = JsonTerminal.Capture(buffer);
        WriteActualSvg(expectedPath, actualSnapshot, null, null);

        if (!File.Exists(expectedPath))
        {
            throw new ShouldAssertException(
                $"Expected snapshot not found: {expectedPath}. Wrote actual snapshot to {GetActualSvgPath(expectedPath)}");
        }

        var expectedJson = ExtractSnapshotJsonFromSvg(File.ReadAllText(expectedPath), expectedPath);
        var expectedSnapshot = JsonSerializer.Deserialize<TerminalSnapshot>(expectedJson) ??
                               throw new ShouldAssertException(
                                    $"Could not deserialize expected snapshot: {expectedPath}");

        var actualJson = JsonTerminal.Serialize(actualSnapshot, false);
        var normalizedExpectedJson = JsonTerminal.Serialize(expectedSnapshot, false);
        actualJson.ShouldBe(normalizedExpectedJson);
    }

    static void WriteActualSvg(string expectedPath,
                               TerminalSnapshot snapshot,
                               int? cursorX,
                               int? cursorY)
    {
        var svgPath = GetActualSvgPath(expectedPath);
        var parent = Path.GetDirectoryName(svgPath);
        if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);

        var svg = RenderSvg(snapshot, cursorX, cursorY);
        File.WriteAllText(svgPath, svg);
    }

    static string RenderSvg(TerminalSnapshot snapshot, int? cursorX, int? cursorY)
    {
        const int cellWidth = 10;
        const int cellHeight = 18;
        const int baseline = 13;

        var widthPx = snapshot.Width * cellWidth;
        var heightPx = snapshot.Height * cellHeight;

        var rows = new TerminalCellSnapshot[snapshot.Height, snapshot.Width];
        foreach (var cell in snapshot.Cells)
        {
            if (cell.X < 0 || cell.X >= snapshot.Width || cell.Y < 0 || cell.Y >= snapshot.Height)
                continue;

            rows[cell.Y, cell.X] = cell;
        }

        var embeddedJson = JsonTerminal.Serialize(snapshot, false);
        var sb = new StringBuilder(capacity: Math.Max(4096, snapshot.Cells.Count * 50));
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{widthPx}\" height=\"{heightPx}\" viewBox=\"0 0 {widthPx} {heightPx}\">");
        sb.AppendLine("  <metadata id=\"thoth-snapshot\" type=\"application/json\"><![CDATA[");
        sb.AppendLine(EscapeCData(embeddedJson));
        sb.AppendLine("  ]]></metadata>");
        sb.AppendLine("  <rect width=\"100%\" height=\"100%\" fill=\"#ffffff\" />");
        sb.AppendLine("  <g font-family=\"ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, Liberation Mono, monospace\" font-size=\"13\" text-rendering=\"optimizeSpeed\">");

        for (var y = 0; y < snapshot.Height; y++)
        {
            for (var x = 0; x < snapshot.Width; x++)
            {
                var cell = rows[y, x];
                if (cell.Width == 0) continue;

                var drawWidth = Math.Max(1, (int)cell.Width) * cellWidth;
                var px = x * cellWidth;
                var py = y * cellHeight;
                var bg = GetPseudoStyleColor(cell.StyleIndex);

                sb.AppendLine($"    <rect x=\"{px}\" y=\"{py}\" width=\"{drawWidth}\" height=\"{cellHeight}\" fill=\"{bg}\" />");

                if (!string.IsNullOrEmpty(cell.Glyph) && cell.Glyph != " ")
                {
                    var glyph = EscapeXml(cell.Glyph);
                    sb.AppendLine($"    <text x=\"{px + 1}\" y=\"{py + baseline}\" fill=\"#111111\">{glyph}</text>");
                }
            }
        }

        AppendCursorOverlay(sb, snapshot.Width, snapshot.Height, cellWidth, cellHeight, cursorX, cursorY);

        sb.AppendLine("  </g>");
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    static string RenderAnimationStepsSvg(IReadOnlyList<AnimationStep> steps, object? metadata)
    {
        const int cellWidth = 10;
        const int cellHeight = 18;
        const int baseline = 13;
        const int rowGap = 2;
        const int leftLabelWidth = 170;

        var maxWidth = 1;
        for (var i = 0; i < steps.Count; i++)
            maxWidth = Math.Max(maxWidth, steps[i].Snapshot.Width);

        var rowHeight = cellHeight + rowGap;
        var widthPx = leftLabelWidth + (maxWidth * cellWidth);
        var renderedRows = 0;
        for (var i = 0; i < steps.Count; i++)
            renderedRows += Math.Max(1, steps[i].Snapshot.Height);
        var heightPx = renderedRows * rowHeight;

        var meta = BuildAnimationMetadata(steps, metadata);
        var embeddedJson = JsonSerializer.Serialize(meta);

        var sb = new StringBuilder(Math.Max(4096, steps.Count * maxWidth * 40));
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{widthPx}\" height=\"{heightPx}\" viewBox=\"0 0 {widthPx} {heightPx}\">");
        sb.AppendLine("  <metadata id=\"thoth-animation\" type=\"application/json\"><![CDATA[");
        sb.AppendLine(EscapeCData(embeddedJson));
        sb.AppendLine("  ]]></metadata>");
        sb.AppendLine("  <rect width=\"100%\" height=\"100%\" fill=\"#ffffff\" />");
        sb.AppendLine("  <g font-family=\"ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, Liberation Mono, monospace\" font-size=\"13\" text-rendering=\"optimizeSpeed\">");

        var renderedRowIndex = 0;
        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            for (var snapshotRow = 0; snapshotRow < Math.Max(1, step.Snapshot.Height); snapshotRow++)
            {
                var rowY = renderedRowIndex * rowHeight;
                var rowLabel = step.Snapshot.Height > 1
                    ? $"{step.Label} line {snapshotRow:00}"
                    : step.Label;
                sb.AppendLine($"    <text x=\"4\" y=\"{rowY + baseline}\" fill=\"#2c3e50\">{EscapeXml(rowLabel)}</text>");

                var rowCells = BuildSnapshotRow(step.Snapshot, snapshotRow);
                for (var x = 0; x < step.Snapshot.Width; x++)
                {
                    var cell = rowCells[x];
                    if (cell.Width == 0) continue;

                    var px = leftLabelWidth + (x * cellWidth);
                    var drawWidth = Math.Max(1, (int)cell.Width) * cellWidth;
                    var bg = GetPseudoStyleColor(cell.StyleIndex);

                    sb.AppendLine($"    <rect x=\"{px}\" y=\"{rowY}\" width=\"{drawWidth}\" height=\"{cellHeight}\" fill=\"{bg}\" />");

                    if (!string.IsNullOrEmpty(cell.Glyph) && cell.Glyph != " ")
                    {
                        var glyph = EscapeXml(cell.Glyph);
                        sb.AppendLine($"    <text x=\"{px + 1}\" y=\"{rowY + baseline}\" fill=\"#111111\">{glyph}</text>");
                    }
                }

                renderedRowIndex++;
            }
        }

        sb.AppendLine("  </g>");
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    static object BuildAnimationMetadata(IReadOnlyList<AnimationStep> steps, object? metadata)
    {
        var stepEntries = new List<object>(steps.Count);
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            stepEntries.Add(new { index = i, label = step.Label, width = step.Snapshot.Width, height = step.Snapshot.Height });
        }

        return new { kind = "thoth.animation.steps", steps = stepEntries, metadata };
    }

    static TerminalCellSnapshot[] BuildSnapshotRow(TerminalSnapshot snapshot, int row)
    {
        var cells = new TerminalCellSnapshot[snapshot.Width];
        for (var i = 0; i < cells.Length; i++)
            cells[i] = new(i, row, 32, 0, 1, " ");

        for (var i = 0; i < snapshot.Cells.Count; i++)
        {
            var cell = snapshot.Cells[i];
            if (cell.Y != row) continue;
            if (cell.X < 0 || cell.X >= snapshot.Width) continue;
            cells[cell.X] = cell;
        }

        return cells;
    }

    static string RenderLayoutDebugSvg(TerminalSnapshot snapshot,
                                       IWidget root,
                                       int width,
                                       int height,
                                       FrameLayoutState? layoutState,
                                       int? cursorX,
                                       int? cursorY)
    {
        const int cellWidth = 10;
        const int cellHeight = 18;
        const int labelOffsetY = 14;
        const int baseline = 13;

        var widthPx = width * cellWidth;
        var heightPx = height * cellHeight;

        var widgets = EnumerateWidgets(root);
        var rows = new TerminalCellSnapshot[snapshot.Height, snapshot.Width];
        foreach (var cell in snapshot.Cells)
        {
            if (cell.X < 0 || cell.X >= snapshot.Width || cell.Y < 0 || cell.Y >= snapshot.Height)
                continue;

            rows[cell.Y, cell.X] = cell;
        }

        var sb = new StringBuilder(Math.Max(4096, widgets.Count * 240));
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{widthPx}\" height=\"{heightPx}\" viewBox=\"0 0 {widthPx} {heightPx}\">");
        sb.AppendLine("  <rect width=\"100%\" height=\"100%\" fill=\"#ffffff\" />");
        sb.AppendLine("  <g font-family=\"ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, Liberation Mono, monospace\" font-size=\"12\">");

        for (var y = 0; y < snapshot.Height; y++)
        {
            for (var x = 0; x < snapshot.Width; x++)
            {
                var cell = rows[y, x];
                if (cell.Width == 0) continue;

                var drawWidth = Math.Max(1, (int)cell.Width) * cellWidth;
                var px = x * cellWidth;
                var py = y * cellHeight;
                var bg = GetPseudoStyleColor(cell.StyleIndex);
                sb.AppendLine($"    <rect x=\"{px}\" y=\"{py}\" width=\"{drawWidth}\" height=\"{cellHeight}\" fill=\"{bg}\" />");

                if (!string.IsNullOrEmpty(cell.Glyph) && cell.Glyph != " ")
                {
                    var glyph = EscapeXml(cell.Glyph);
                    sb.AppendLine($"    <text x=\"{px + 1}\" y=\"{py + baseline}\" fill=\"#333333\">{glyph}</text>");
                }
            }
        }

        AppendCursorOverlay(sb, snapshot.Width, snapshot.Height, cellWidth, cellHeight, cursorX, cursorY);

        for (var i = 0; i < widgets.Count; i++)
        {
            var widget = widgets[i];
            if (!TryGetRect(layoutState, widget, out var rect)) continue;
            if (rect.Width <= 0 || rect.Height <= 0) continue;

            var x = rect.X * cellWidth;
            var y = rect.Y * cellHeight;
            var w = Math.Max(1, rect.Width * cellWidth);
            var h = Math.Max(1, rect.Height * cellHeight);
            var color = ColorForIndex(i);
            var label = EscapeXml($"{i}:{widget.GetType().Name}");

            sb.AppendLine($"    <rect x=\"{x}\" y=\"{y}\" width=\"{w}\" height=\"{h}\" fill=\"{color.fill}\" stroke=\"{color.stroke}\" stroke-width=\"2\" fill-opacity=\"0.35\" />");
            sb.AppendLine($"    <text x=\"{x + 3}\" y=\"{y + labelOffsetY}\" fill=\"#111111\">{label}</text>");
        }

        sb.AppendLine("  </g>");
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    static string RenderInvalidationOverlaySvg(IWidget root,
                                               int width,
                                               int height,
                                               FrameLayoutState? layoutState,
                                               IReadOnlyDictionary<IWidget, InvalidationKind>? invalidations)
    {
        const int cellWidth = 10;
        const int cellHeight = 18;
        const int labelOffsetY = 14;

        var widthPx = width * cellWidth;
        var heightPx = height * cellHeight;
        var widgets = EnumerateWidgets(root);

        var sb = new StringBuilder(Math.Max(2048, widgets.Count * 160));
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{widthPx}\" height=\"{heightPx}\" viewBox=\"0 0 {widthPx} {heightPx}\">");
        sb.AppendLine("  <rect width=\"100%\" height=\"100%\" fill=\"#ffffff\" />");
        sb.AppendLine("  <g font-family=\"ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, Liberation Mono, monospace\" font-size=\"12\">");

        for (var i = 0; i < widgets.Count; i++)
        {
            var widget = widgets[i];
            if (!TryGetRect(layoutState, widget, out var rect)) continue;
            if (rect.Width <= 0 || rect.Height <= 0) continue;

            var kind = invalidations is null || !invalidations.TryGetValue(widget, out var value)
                ? InvalidationKind.None
                : value;
            if (kind == InvalidationKind.None) continue;

            var color = kind.HasFlag(InvalidationKind.Layout)
                ? (fill: "#93c5fd", stroke: "#1d4ed8", label: "layout")
                : (fill: "#fdba74", stroke: "#c2410c", label: "content");

            var x = rect.X * cellWidth;
            var y = rect.Y * cellHeight;
            var w = Math.Max(1, rect.Width * cellWidth);
            var h = Math.Max(1, rect.Height * cellHeight);
            var label = EscapeXml($"{i}:{widget.GetType().Name}:{color.label}");

            sb.AppendLine($"    <rect x=\"{x}\" y=\"{y}\" width=\"{w}\" height=\"{h}\" fill=\"{color.fill}\" stroke=\"{color.stroke}\" stroke-width=\"2\" fill-opacity=\"0.45\" />");
            sb.AppendLine($"    <text x=\"{x + 3}\" y=\"{y + labelOffsetY}\" fill=\"#111111\">{label}</text>");
        }

        sb.AppendLine("  </g>");
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    static void AppendCursorOverlay(StringBuilder sb,
                                    int width,
                                    int height,
                                    int cellWidth,
                                    int cellHeight,
                                    int? cursorX,
                                    int? cursorY)
    {
        if (cursorX is not { } x || cursorY is not { } y) return;
        if (x < 0 || x >= width || y < 0 || y >= height) return;

        var px = x * cellWidth;
        var py = y * cellHeight;
        sb.AppendLine($"    <rect x=\"{px + 1}\" y=\"{py + 1}\" width=\"{cellWidth - 2}\" height=\"{cellHeight - 2}\" fill=\"none\" stroke=\"#f6b73c\" stroke-width=\"2\" />");
    }

    static List<IWidget> EnumerateWidgets(IWidget root)
    {
        var list = new List<IWidget>(64);
        var stack = new Stack<IWidget>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var widget = stack.Pop();
            list.Add(widget);
            var visitor = new PushToStackVisitor(stack);
            WidgetTraversalExtensions.VisitChildrenReverse(widget, ref visitor);
        }

        return list;
    }

    static bool TryGetRect(FrameLayoutState? layoutState, IWidget widget, out Rect rect)
    {
        if (layoutState is not null) return layoutState.TryGetRect(widget, out rect);

        rect = default;
        return false;
    }

    static (string fill, string stroke) ColorForIndex(int index)
    {
        var hue = Math.Abs(index * 47) % 360;
        return ($"hsl({hue}, 82%, 68%)", $"hsl({hue}, 76%, 42%)");
    }

    static string GetPseudoStyleColor(int styleIndex)
    {
        if (styleIndex == 0) return "#ffffff";
        var hue = Math.Abs(styleIndex * 47) % 360;
        return $"hsl({hue}, 62%, 82%)";
    }

    static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }

    static string EscapeCData(string value)
    {
        return value.Replace("]]>", "]]]]><![CDATA[>", StringComparison.Ordinal);
    }

    static string ExtractSnapshotJsonFromSvg(string svg, string expectedPath)
    {
        const string metadataStart = "<metadata id=\"thoth-snapshot\" type=\"application/json\">";
        const string cdataStart = "<![CDATA[";
        const string cdataEnd = "]]></metadata>";

        var metadataIndex = svg.IndexOf(metadataStart, StringComparison.Ordinal);
        if (metadataIndex < 0)
            throw new ShouldAssertException($"Snapshot metadata block not found in expected SVG: {expectedPath}");

        var cdataStartIndex = svg.IndexOf(cdataStart, metadataIndex, StringComparison.Ordinal);
        if (cdataStartIndex < 0)
            throw new ShouldAssertException($"Snapshot metadata CDATA start not found in expected SVG: {expectedPath}");

        cdataStartIndex += cdataStart.Length;
        var cdataEndIndex = svg.IndexOf(cdataEnd, cdataStartIndex, StringComparison.Ordinal);
        if (cdataEndIndex < 0)
            throw new ShouldAssertException($"Snapshot metadata CDATA end not found in expected SVG: {expectedPath}");

        return svg[cdataStartIndex..cdataEndIndex].Replace("]]]]><![CDATA[>", "]]>", StringComparison.Ordinal);
    }

    static string GetActualSvgPath(string expectedPath)
    {
        var directory = Path.GetDirectoryName(expectedPath) ?? ".";
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(expectedPath);
        return Path.Combine(directory, $"{fileNameWithoutExtension}.actual.svg");
    }

    static string GetOutlinesSvgPath(string expectedPath)
    {
        var directory = Path.GetDirectoryName(expectedPath) ?? ".";
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(expectedPath);
        return Path.Combine(directory, $"{fileNameWithoutExtension}.outlines.svg");
    }

    static string GetInvalidationSvgPath(string expectedPath)
    {
        var directory = Path.GetDirectoryName(expectedPath) ?? ".";
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(expectedPath);
        return Path.Combine(directory, $"{fileNameWithoutExtension}.invalidation.svg");
    }

    static string RenderTrueColorAnimatedSvg(IReadOnlyList<TrueColorInteractionFrame> frames,
                                              RenderContext context)
    {
        const int cellWidth = 10;
        const int cellHeight = 18;
        const int baseline = 13;
        var first = frames[0].Snapshot;
        var widthPx = first.Width * cellWidth;
        var heightPx = first.Height * cellHeight;

        var sb = new StringBuilder(Math.Max(4096, frames.Count * first.Width * first.Height * 30));
        sb.AppendLine(
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{widthPx}\" height=\"{heightPx}\" viewBox=\"0 0 {widthPx} {heightPx}\">");

        if (frames.Count > 1)
            AppendAnimationStyles(sb, frames);

        sb.AppendLine("  <rect width=\"100%\" height=\"100%\" fill=\"#0d1117\"/>");
        sb.AppendLine(
            "  <g font-family=\"ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, Liberation Mono, monospace\" font-size=\"13\" text-rendering=\"optimizeSpeed\">");

        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames[i];
            var cls = frames.Count > 1 ? $" class=\"thoth-f{i}\"" : "";
            sb.AppendLine($"    <g{cls}>");
            AppendFrameCells(sb, frame.Snapshot, context, cellWidth, cellHeight, baseline, first.Height);
            if (frame.MouseX is { } mx && frame.MouseY is { } my)
                AppendMouseCursor(sb, mx * cellWidth, my * cellHeight);
            sb.AppendLine("    </g>");
        }

        sb.AppendLine("  </g>");
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    static void AppendAnimationStyles(StringBuilder sb, IReadOnlyList<TrueColorInteractionFrame> frames)
    {
        var totalMs = 0;
        for (var i = 0; i < frames.Count; i++) totalMs += frames[i].DurationMs;
        var totalSecs = totalMs / 1000.0;

        sb.AppendLine("  <style>");

        for (var i = 0; i < frames.Count; i++)
            sb.AppendLine($"    .thoth-f{i} {{ animation: thoth-kf{i} {totalSecs:F1}s linear infinite; }}");

        var startMs = 0;
        for (var i = 0; i < frames.Count; i++)
        {
            var endMs = startMs + frames[i].DurationMs;
            var startPct = startMs * 100.0 / totalMs;
            var endPct = endMs * 100.0 / totalMs;

            sb.Append($"    @keyframes thoth-kf{i} {{ ");
            if (i == 0)
            {
                sb.Append(
                    $"0%,{endPct:F2}%{{opacity:1}} {endPct + 0.01:F2}%,100%{{opacity:0}}");
            }
            else if (i == frames.Count - 1)
            {
                sb.Append(
                    $"0%,{startPct:F2}%{{opacity:0}} {startPct + 0.01:F2}%,100%{{opacity:1}}");
            }
            else
            {
                sb.Append(
                    $"0%,{startPct:F2}%{{opacity:0}} {startPct + 0.01:F2}%,{endPct:F2}%{{opacity:1}} {endPct + 0.01:F2}%,100%{{opacity:0}}");
            }

            sb.AppendLine(" }");
            startMs = endMs;
        }

        sb.AppendLine("  </style>");
    }

    static void AppendFrameCells(StringBuilder sb,
                                  TerminalSnapshot snapshot,
                                  RenderContext context,
                                  int cellWidth,
                                  int cellHeight,
                                  int baseline,
                                  int maxRows)
    {
        const string defaultFg = "#e0d2ae";
        const string defaultBg = "#0d1117";

        var rows = new TerminalCellSnapshot[snapshot.Height, snapshot.Width];
        for (var i = 0; i < snapshot.Cells.Count; i++)
        {
            var cell = snapshot.Cells[i];
            if (cell.X < 0 || cell.X >= snapshot.Width || cell.Y < 0 || cell.Y >= snapshot.Height)
                continue;
            rows[cell.Y, cell.X] = cell;
        }

        var xBuf = new StringBuilder(128);
        var gBuf = new StringBuilder(64);

        for (var y = 0; y < maxRows; y++)
        {
            var py = y * cellHeight;

            // Pass 1: background rects merged into same-color horizontal runs
            var x = 0;
            while (x < snapshot.Width)
            {
                var cell = rows[y, x];
                if (cell.Width == 0) { x++; continue; }

                context.Styles.TryGet(cell.StyleIndex, out var style);
                var bg = style.Background.HasValue ? ToHex(style.Background.Value) : defaultBg;
                if (bg == defaultBg) { x += Math.Max(1, (int)cell.Width); continue; }

                var runStart = x;
                x += Math.Max(1, (int)cell.Width);
                while (x < snapshot.Width)
                {
                    var next = rows[y, x];
                    if (next.Width == 0) { x++; continue; }
                    context.Styles.TryGet(next.StyleIndex, out var ns);
                    var nextBg = ns.Background.HasValue ? ToHex(ns.Background.Value) : defaultBg;
                    if (nextBg != bg) break;
                    x += Math.Max(1, (int)next.Width);
                }

                sb.AppendLine(
                    $"      <rect x=\"{runStart * cellWidth}\" y=\"{py}\" width=\"{(x - runStart) * cellWidth}\" height=\"{cellHeight}\" fill=\"{bg}\"/>");
            }

            // Pass 2: glyphs merged into same-fg runs with explicit per-glyph x positions
            string? currentFg = null;
            xBuf.Clear();
            gBuf.Clear();
            var textY = py + baseline;

            for (x = 0; x < snapshot.Width;)
            {
                var cell = rows[y, x];
                if (cell.Width == 0) { x++; continue; }

                var cw = Math.Max(1, (int)cell.Width);
                var hasGlyph = !string.IsNullOrEmpty(cell.Glyph) && cell.Glyph != " ";
                if (!hasGlyph) { x += cw; continue; }

                context.Styles.TryGet(cell.StyleIndex, out var style);
                var fg = style.Foreground.HasValue ? ToHex(style.Foreground.Value) : defaultFg;

                if (fg != currentFg)
                {
                    if (gBuf.Length > 0)
                    {
                        sb.AppendLine($"      <text x=\"{xBuf}\" y=\"{textY}\" fill=\"{currentFg}\">{gBuf}</text>");
                        xBuf.Clear();
                        gBuf.Clear();
                    }
                    currentFg = fg;
                }

                if (xBuf.Length > 0) xBuf.Append(' ');
                xBuf.Append(x * cellWidth + 1);
                gBuf.Append(EscapeXml(cell.Glyph!));
                x += cw;
            }

            if (gBuf.Length > 0)
                sb.AppendLine($"      <text x=\"{xBuf}\" y=\"{textY}\" fill=\"{currentFg}\">{gBuf}</text>");
        }
    }

    static void AppendMouseCursor(StringBuilder sb, int px, int py)
    {
        sb.AppendLine(
            $"      <polygon transform=\"translate({px},{py})\" points=\"0,0 0,14 4,10 7,16 10,14 7,8 12,8\" fill=\"#ffffff\" stroke=\"#000000\" stroke-width=\"1\"/>");
    }

    static string ToHex(Color c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";
}
