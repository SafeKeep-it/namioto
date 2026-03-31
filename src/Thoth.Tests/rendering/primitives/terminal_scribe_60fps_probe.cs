using System.Diagnostics;
using System.Text;
using Thoth.Rendering;
using Thoth.Terminal.Raw.Egress;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.canvas_rendering;

public class terminal_scribe_60fps_probe
{
    [Fact]
    public void measures_full_vs_partial_on_large_animated_surface()
    {
        if (Environment.GetEnvironmentVariable("THOTH_RUN_PERF") != "1") return;

        const int width = 160;
        const int height = 48;
        const int frames = 180;

        var mode = ResolveMode();

        if (mode == "full")
        {
            var fullOnly = RunScenario("full", width, height, frames, forceFullEveryFrame: true);
            Console.WriteLine($"terminal_scribe_60fps_probe mode=full elapsed_ms={fullOnly.elapsed.TotalMilliseconds:F2} bytes={fullOnly.bytes} fps={(frames / fullOnly.elapsed.TotalSeconds):F2}");
            return;
        }

        if (mode == "partial")
        {
            var partialOnly = RunScenario("partial", width, height, frames, forceFullEveryFrame: false);
            Console.WriteLine($"terminal_scribe_60fps_probe mode=partial elapsed_ms={partialOnly.elapsed.TotalMilliseconds:F2} bytes={partialOnly.bytes} fps={(frames / partialOnly.elapsed.TotalSeconds):F2}");
            return;
        }

        var full = RunScenario("full", width, height, frames, forceFullEveryFrame: true);
        var partial = RunScenario("partial", width, height, frames, forceFullEveryFrame: false);

        var fullMs = full.elapsed.TotalMilliseconds;
        var partialMs = partial.elapsed.TotalMilliseconds;
        var delta = fullMs <= 0 ? 0 : (fullMs - partialMs) / fullMs * 100.0;

        Console.WriteLine($"terminal_scribe_60fps_probe full={fullMs:F2}ms bytes={full.bytes}");
        Console.WriteLine($"terminal_scribe_60fps_probe partial={partialMs:F2}ms bytes={partial.bytes}");
        Console.WriteLine($"terminal_scribe_60fps_probe delta_vs_full={delta:+0.00;-0.00;0.00}%");
    }

    static string ResolveMode()
    {
        var probeMode = Environment.GetEnvironmentVariable("THOTH_PROBE_MODE");
        if (!string.IsNullOrWhiteSpace(probeMode)) return probeMode.Trim().ToLowerInvariant();

        var renderMode = Environment.GetEnvironmentVariable("THOTH_RENDER_MODE");
        if (string.Equals(renderMode, "full", StringComparison.OrdinalIgnoreCase)) return "full";
        if (string.Equals(renderMode, "partial", StringComparison.OrdinalIgnoreCase)) return "partial";

        return "both";
    }

    static (TimeSpan elapsed, long bytes) RunScenario(string mode,
                                                      int width,
                                                      int height,
                                                      int frames,
                                                      bool forceFullEveryFrame)
    {
        var begin = DateTimeOffset.UtcNow;
        Console.WriteLine($"ANIMATION_BEGIN mode={mode} utc={begin:O} frames={frames} size={width}x{height}");

        var terminal = new MockTerminal { WindowWidth = width, WindowHeight = height };
        var scribe = new TerminalScribe(terminal);
        var root = new Screen();
        var context = new RenderContext(new UiContext(root));
        var buffer = new ScreenBuffer(width, height);

        var backgroundStyle = new Style(new Color(220, 220, 220), new Color(20, 20, 25));
        var logoStyle = new Style(new Color(15, 15, 15), new Color(232, 205, 140));

        ushort frame = 1;
        var canvas = new Canvas(buffer, new Rect(0, 0, width, height), context, frameNumber: frame);
        canvas.Fill(0, 0, width, height, (Rune)' ', backgroundStyle);

        var logo = new[]
        {
            "   ____  ",
            "  / __ \\",
            " / /_/ /  EGYPTIAN SCRIBE",
            "/_____/",
            "  /|    stylus",
            " /_|_"
        };

        var prevX = 0;
        var prevY = 8;
        var maxX = Math.Max(1, width - 30);

        scribe.Render(buffer, context, frame, renderFullFrame: true);
        long totalBytes = terminal.DrainWrittenBytes().Length;

        var sw = Stopwatch.StartNew();

        for (var i = 1; i < frames; i++)
        {
            frame++;
            var x = i % maxX;
            var y = prevY + ((i / 20) % 2 == 0 ? 0 : 1);

            var eraseCanvas = new Canvas(buffer, new Rect(0, 0, width, height), context, frameNumber: frame);
            for (var row = 0; row < logo.Length; row++)
                eraseCanvas.DrawString(prevX, prevY + row, logo[row], backgroundStyle);

            var drawCanvas = new Canvas(buffer, new Rect(0, 0, width, height), context, frameNumber: frame);
            for (var row = 0; row < logo.Length; row++)
                drawCanvas.DrawString(x, y + row, logo[row], logoStyle);

            var renderFullFrame = forceFullEveryFrame || i == 1;
            scribe.Render(buffer, context, frame, renderFullFrame);
            totalBytes += terminal.DrainWrittenBytes().Length;

            prevX = x;
            prevY = y;
        }

        sw.Stop();
        var end = DateTimeOffset.UtcNow;
        Console.WriteLine($"ANIMATION_END mode={mode} utc={end:O} elapsed_ms={sw.Elapsed.TotalMilliseconds:F2} bytes={totalBytes} fps={(frames / sw.Elapsed.TotalSeconds):F2}");
        return (sw.Elapsed, totalBytes);
    }
}
