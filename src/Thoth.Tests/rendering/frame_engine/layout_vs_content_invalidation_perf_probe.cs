using System.Diagnostics;
using Thoth.Rendering;
using Thoth.Rendering.Grid;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace comptatata.tests.app.cli.thoth.rendering.frame_engine;

public class layout_vs_content_invalidation_perf_probe
{
    [Fact]
    public void measures_frame_time_for_layout_vs_content_invalidation()
    {
        if (Environment.GetEnvironmentVariable("THOTH_RUN_PERF") != "1") return;

        const int width = 120;
        const int height = 40;
        const int frames = 300;

        var root = BuildTree();
        var uiContext = new UiContext(root);
        var draw = new capture_and_draw_strategy();
        var engine = new FrameRenderer(fullRender: false, drawStrategy: draw);

        var (_, _, _) = engine.RenderFrame(root,
                                           uiContext,
                                           width,
                                           height,
                                           new Dictionary<IWidget, InvalidationKind>());

        var probeLeaf = root.ProbeLeaf;

        var content = RunScenario(engine,
                                  uiContext,
                                  draw,
                                  width,
                                  height,
                                  frames,
                                  probeLeaf,
                                  InvalidationKind.Content,
                                  "layout_vs_content_invalidation_perf_probe.content.svg");

        var layout = RunScenario(engine,
                                 uiContext,
                                 draw,
                                 width,
                                 height,
                                 frames,
                                 probeLeaf,
                                 InvalidationKind.Layout,
                                 "layout_vs_content_invalidation_perf_probe.layout.svg");

        Console.WriteLine($"PERF_SCENARIO content ms_total={content.Elapsed.TotalMilliseconds:F2} ms_frame={content.MsPerFrame:F4} fps={content.Fps:F2} p50={content.P50Ms:F4} p95={content.P95Ms:F4} p99={content.P99Ms:F4} tm99={content.Tm99Ms:F4}");
        Console.WriteLine($"PERF_SCENARIO layout ms_total={layout.Elapsed.TotalMilliseconds:F2} ms_frame={layout.MsPerFrame:F4} fps={layout.Fps:F2} p50={layout.P50Ms:F4} p95={layout.P95Ms:F4} p99={layout.P99Ms:F4} tm99={layout.Tm99Ms:F4}");

        var delta = content.MsPerFrame <= 0
            ? 0
            : ((layout.MsPerFrame - content.MsPerFrame) / content.MsPerFrame) * 100.0;
        Console.WriteLine($"PERF_DELTA layout_vs_content_percent={delta:+0.00;-0.00;0.00}");
    }

    static ScenarioResult RunScenario(FrameRenderer engine,
                                      UiContext uiContext,
                                      capture_and_draw_strategy draw,
                                      int width,
                                      int height,
                                      int frames,
                                      IWidget probeLeaf,
                                      InvalidationKind invalidationKind,
                                      string snapshot)
    {
        GridBuffer lastBuffer = null!;
        var frameTicks = new long[Math.Max(1, frames)];
        var sw = Stopwatch.StartNew();

        for (var i = 0; i < frames; i++)
        {
            var frameStart = Stopwatch.GetTimestamp();
            var invalidations = new Dictionary<IWidget, InvalidationKind>
                                {
                                    [probeLeaf] = invalidationKind
                                };

            (lastBuffer, _, _) = engine.RenderFrame(probeLeaf.Parent,
                                                    uiContext,
                                                    width,
                                                    height,
                                                    invalidations);
            frameTicks[i] = Stopwatch.GetTimestamp() - frameStart;
        }

        sw.Stop();

        lastBuffer.WriteTerminalSnapshotSvg(snapshot);
        lastBuffer.WriteLayoutDebugSvg(probeLeaf.Parent,
                                       width,
                                       height,
                                       snapshot,
                                       engine.LayoutState);
        terminal_snapshot_assertions.WriteInvalidationOverlaySvg(probeLeaf.Parent,
                                                                 width,
                                                                 height,
                                                                 snapshot,
                                                                 engine.LayoutState,
                                                                 draw.LastInvalidations);

        var msPerFrame = sw.Elapsed.TotalMilliseconds / Math.Max(1, frames);
        var fps = frames / Math.Max(0.000001, sw.Elapsed.TotalSeconds);
        var stats = FrameStats.FromTicks(frameTicks);
        return new(sw.Elapsed, msPerFrame, fps, stats.P50Ms, stats.P95Ms, stats.P99Ms, stats.Tm99Ms);
    }

    static probe_root BuildTree()
    {
        var root = new probe_root();
        for (var i = 0; i < 120; i++)
        {
            var text = new TextBlock { Text = $"node-{i:000}-thoth" };
            var border = new Border { Content = text };
            root.Add(border);
            if (i == 60) root.ProbeLeaf = text;
        }

        return root;
    }

    sealed class probe_root : Screen
    {
        public IWidget ProbeLeaf { get; set; } = SentinelWidget.Instance;
    }

    sealed class capture_and_draw_strategy : IFrameDrawStrategy
    {
        readonly ScribeFrameDrawStrategy _inner = new();
        public IReadOnlyDictionary<IWidget, InvalidationKind>? LastInvalidations { get; private set; }

        public void Draw(IWidget root,
                         UiContext uiContext,
                         GridBuffer buffer,
                         IReadOnlyDictionary<IWidget, InvalidationKind>? invalidations,
                         ushort frameNumber,
                         FrameLayoutState layoutState)
        {
            LastInvalidations = invalidations;
            _inner.Draw(root, uiContext, buffer, invalidations, frameNumber, layoutState);
        }
    }

    readonly record struct ScenarioResult(TimeSpan Elapsed,
                                          double MsPerFrame,
                                          double Fps,
                                          double P50Ms,
                                          double P95Ms,
                                          double P99Ms,
                                          double Tm99Ms);

    readonly record struct FrameStats(double P50Ms, double P95Ms, double P99Ms, double Tm99Ms)
    {
        public static FrameStats FromTicks(long[] ticks)
        {
            if (ticks.Length == 0) return new(0, 0, 0, 0);

            var sorted = (long[])ticks.Clone();
            Array.Sort(sorted);

            var p50 = Percentile(sorted, 50);
            var p95 = Percentile(sorted, 95);
            var p99 = Percentile(sorted, 99);
            var tm99 = TailMean(sorted, 99);

            return new(ToMs(p50), ToMs(p95), ToMs(p99), ToMs(tm99));
        }

        static long Percentile(long[] sorted, int percentile)
        {
            var index = (int)Math.Ceiling((percentile / 100.0) * sorted.Length) - 1;
            return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
        }

        static double TailMean(long[] sorted, int percentile)
        {
            var start = (int)Math.Ceiling((percentile / 100.0) * sorted.Length) - 1;
            start = Math.Clamp(start, 0, sorted.Length - 1);

            long total = 0;
            for (var i = start; i < sorted.Length; i++)
                total += sorted[i];

            return (double)total / Math.Max(1, sorted.Length - start);
        }

        static double ToMs(double ticks) => ticks * 1000.0 / Stopwatch.Frequency;
    }
}
