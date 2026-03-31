using System.Diagnostics;
using System.Text;
using Thoth.Rendering;
using Thoth.Terminal.Raw.Egress;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.canvas_rendering;

public class terminal_scribe_real_ui_surface_probe
{
    [Fact]
    public void measures_border_and_blank_heavy_surface_with_rle()
    {
        if (Environment.GetEnvironmentVariable("THOTH_RUN_PERF") != "1") return;

        const int width = 160;
        const int height = 48;
        const int frames = 240;

        var result = RunScenario(width, height, frames);

        Console.WriteLine("PERF_MODE scene=ui_surface mode=rle_default");
        Console.WriteLine($"PERF_RESULT scene=ui_surface mode=rle_default elapsed_ms={result.Elapsed.TotalMilliseconds:F2} bytes={result.Bytes} ms_frame={result.Stats.MeanMs:F4} p50={result.Stats.P50Ms:F4} p95={result.Stats.P95Ms:F4} p99={result.Stats.P99Ms:F4} tm99={result.Stats.Tm99Ms:F4}");
    }

    static ScenarioResult RunScenario(int width, int height, int frames)
    {
        var terminal = new MockTerminal { WindowWidth = width, WindowHeight = height };
        var scribe = new TerminalScribe(terminal);
        var root = new Screen();
        var context = new RenderContext(new UiContext(root));
        var buffer = new ScreenBuffer(width, height);

        var frameTimes = new List<double>(frames);
        long totalBytes = 0;
        ushort frame = 1;

        DrawFrame(buffer, context, frame, width, height, tick: 0);

        var total = Stopwatch.StartNew();
        for (var i = 0; i < frames; i++)
        {
            frame++;
            DrawFrame(buffer, context, frame, width, height, i);

            var frameSw = Stopwatch.StartNew();
            scribe.Render(buffer, context, frame, renderFullFrame: true);
            frameSw.Stop();

            frameTimes.Add(frameSw.Elapsed.TotalMilliseconds);
            totalBytes += terminal.DrainWrittenBytes().Length;
        }

        total.Stop();
        return new(total.Elapsed, totalBytes, FrameStats.From(frameTimes));
    }

    static void DrawFrame(ScreenBuffer buffer,
                          RenderContext context,
                          ushort frame,
                          int width,
                          int height,
                          int tick)
    {
        var background = new Style(new Color(210, 210, 210), new Color(16, 18, 24));
        var panel = new Style(new Color(232, 234, 240), new Color(26, 30, 40));
        var border = new Style(new Color(180, 190, 220), new Color(26, 30, 40));
        var accent = new Style(new Color(20, 20, 24), new Color(215, 190, 120));

        var canvas = new Canvas(buffer, new Rect(0, 0, width, height), context, frameNumber: frame);
        canvas.Fill(0, 0, width, height, (Rune)' ', background);

        var leftWidth = Math.Max(24, width / 5);
        var rightWidth = Math.Max(26, width / 4);
        var headerHeight = 3;
        var footerHeight = 2;

        DrawPanel(canvas, 0, 0, width, headerHeight, border, panel);
        DrawPanel(canvas, 0, headerHeight, leftWidth, height - headerHeight - footerHeight, border, panel);
        DrawPanel(canvas,
                  leftWidth,
                  headerHeight,
                  width - leftWidth - rightWidth,
                  height - headerHeight - footerHeight,
                  border,
                  panel);
        DrawPanel(canvas,
                  width - rightWidth,
                  headerHeight,
                  rightWidth,
                  height - headerHeight - footerHeight,
                  border,
                  panel);
        DrawPanel(canvas, 0, height - footerHeight, width, footerHeight, border, panel);

        var blink = (tick / 12) % 2 == 0 ? ' ' : 'X';
        var spinner = "|/-\\"[tick % 4];
        canvas.DrawString(3, 1, "THOTH DASHBOARD", accent);
        canvas.DrawString(width - 18, 1, $"sync {spinner}", accent);
        canvas.DrawString(leftWidth + 3, headerHeight + 2, $"cursor [{blink}]", accent);
        canvas.DrawString(leftWidth + 3, headerHeight + 4, "status: collecting telemetry", accent);

        var listRows = Math.Min(18, height - headerHeight - footerHeight - 4);
        for (var i = 0; i < listRows; i++)
        {
            var marker = i == tick % Math.Max(1, listRows) ? '>' : ' ';
            canvas.DrawString(2, headerHeight + 2 + i, $"{marker} task-{i + 1:00}", panel);
        }
    }

    static void DrawPanel(Canvas canvas, int x, int y, int width, int height, Style border, Style fill)
    {
        if (width <= 0 || height <= 0) return;

        canvas.Fill(x, y, width, height, (Rune)' ', fill);

        if (width < 2 || height < 2) return;

        canvas.DrawString(x, y, new string('─', width), border);
        canvas.DrawString(x, y + height - 1, new string('─', width), border);

        for (var row = y + 1; row < y + height - 1; row++)
        {
            canvas.PutGlyph(x, row, (Rune)'│', border);
            canvas.PutGlyph(x + width - 1, row, (Rune)'│', border);
        }

        canvas.PutGlyph(x, y, (Rune)'┌', border);
        canvas.PutGlyph(x + width - 1, y, (Rune)'┐', border);
        canvas.PutGlyph(x, y + height - 1, (Rune)'└', border);
        canvas.PutGlyph(x + width - 1, y + height - 1, (Rune)'┘', border);
    }

    readonly record struct ScenarioResult(TimeSpan Elapsed, long Bytes, FrameStats Stats);

    readonly record struct FrameStats(double MeanMs,
                                      double P50Ms,
                                      double P95Ms,
                                      double P99Ms,
                                      double Tm99Ms)
    {
        public static FrameStats From(List<double> ms)
        {
            if (ms.Count == 0) return new(0, 0, 0, 0, 0);

            var ordered = ms.OrderBy(v => v).ToArray();
            var mean = ms.Average();
            var p50 = Percentile(ordered, 0.50);
            var p95 = Percentile(ordered, 0.95);
            var p99 = Percentile(ordered, 0.99);
            var tailCount = Math.Max(1, (int)Math.Ceiling(ordered.Length * 0.01));
            var tm99 = ordered.Skip(ordered.Length - tailCount).Average();

            return new(mean, p50, p95, p99, tm99);
        }

        static double Percentile(double[] ordered, double p)
        {
            if (ordered.Length == 0) return 0;

            var index = (ordered.Length - 1) * p;
            var lo = (int)Math.Floor(index);
            var hi = (int)Math.Ceiling(index);
            if (lo == hi) return ordered[lo];

            var t = index - lo;
            return ordered[lo] + ((ordered[hi] - ordered[lo]) * t);
        }
    }
}
