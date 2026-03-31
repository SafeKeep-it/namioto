using System.Diagnostics;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Navigation.Focus;
using Thoth.Rendering;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Thoth.Tests.eventing.perf;

public class indexed_runtime_paths_perf_probe
{
    [Fact]
    public void compares_indexed_paths_against_baselines()
    {
        var perfMode = Environment.GetEnvironmentVariable("THOTH_RUN_PERF");
        var runBaselineOnly = perfMode == "0";
        var runCompare = perfMode == "1";
        if (!runBaselineOnly && !runCompare) return;

        const int width = 160;
        const int height = 50;
        const int widgets = 900;
        const int samples = 5000;

        var (root, nodes) = BuildGrid(widgets, width, height);

        var engine = new FrameRenderer(fullRender: false);
        _ = engine.RenderFrame(root, new(root), width, height, new Dictionary<IWidget, InvalidationKind>());

        var layout = engine.LayoutState;
        var points = BuildPoints(samples, width, height);

        var baselineHit = Measure(samples, i => BaselineHitTest(nodes, points[i].X, points[i].Y));
        var indexedHit = Measure(samples, i => layout.WidgetAt(points[i].X, points[i].Y));

        var current = nodes[nodes.Count / 2];
        var baselineFocus = Measure(samples, _ => BaselineFocusableCandidates(root, current, layout));
        var indexedFocus = Measure(samples, _ => IndexedFocusableCandidates(layout, current));

        var dispatcher = new EventDispatcher();
        dispatcher.SetLayoutState(layout);
        var target = BuildRouteChain(64);

        var baselineRoutes = Measure(samples, _ => BaselineRouteBuild<OnMouseMove>(target));

        Console.WriteLine($"PERF_MODE {(runBaselineOnly ? "baseline" : "compare")}");
        WriteScenario("hit_test.baseline", baselineHit);
        WriteScenario("focus.baseline", baselineFocus);
        WriteScenario("route.baseline", baselineRoutes);

        if (runBaselineOnly) return;

        var indexedRoutes = Measure(samples, _ => dispatcher.GetBubblePath<OnMouseMove>(target));

        WriteScenario("hit_test.indexed", indexedHit);
        WriteScenario("focus.indexed", indexedFocus);
        WriteScenario("route.indexed", indexedRoutes);

        WriteDelta("hit_test", baselineHit, indexedHit);
        WriteDelta("focus", baselineFocus, indexedFocus);
        WriteDelta("route", baselineRoutes, indexedRoutes);
    }

    static (grid_root Root, List<layout_node> Nodes) BuildGrid(int count, int width, int height)
    {
        var root = new grid_root(width, height);
        var columns = Math.Max(1, width / 4);
        var rows = Math.Max(1, height / 2);
        var cappedCount = Math.Min(count, columns * rows);

        var nodes = new List<layout_node>(cappedCount);
        for (var i = 0; i < cappedCount; i++)
        {
            var column = i % columns;
            var row = i / columns;
            var node = new layout_node(new(column * 4, row * 2, 3, 1));
            nodes.Add(node);
            root.Add(node);
        }

        root.SetNodes(nodes);
        return (root, nodes);
    }

    static (int X, int Y)[] BuildPoints(int count, int width, int height)
    {
        var random = new Random(42);
        var points = new (int X, int Y)[count];
        for (var i = 0; i < count; i++)
            points[i] = (random.Next(0, width), random.Next(0, height));
        return points;
    }

    static IWidget BaselineHitTest(List<layout_node> nodes, int x, int y)
    {
        IWidget best = SentinelWidget.Instance;
        for (var i = 0; i < nodes.Count; i++)
        {
            var rect = nodes[i].Rect;
            if (x < rect.X || x >= rect.X + rect.Width) continue;
            if (y < rect.Y || y >= rect.Y + rect.Height) continue;
            best = nodes[i];
        }

        return best;
    }

    static List<IWidget> BaselineFocusableCandidates(IWidget root, IWidget current, FrameLayoutState layout)
    {
        var stack = new Stack<IWidget>();
        var candidates = new List<IWidget>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var widget = stack.Pop();
            if (!ReferenceEquals(widget, current) &&
                widget is IFocusable &&
                layout.TryGetRect(widget, out _))
                candidates.Add(widget);

            var visitor = new PushToStackVisitor(stack);
            WidgetTraversalExtensions.VisitChildrenReverse(widget, ref visitor);
        }

        return candidates;
    }

    static List<IWidget> IndexedFocusableCandidates(FrameLayoutState layout, IWidget current)
    {
        var items = layout.FocusableItems();
        var candidates = new List<IWidget>(items.Count);

        for (var i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i].Widget, current)) continue;
            candidates.Add(items[i].Widget);
        }

        return candidates;
    }

    static List<IWidget> BaselineRouteBuild<T>(IWidget target) where T : struct
    {
        var ancestors = new List<IWidget>();
        var current = target;
        while (current is not SentinelWidget)
        {
            ancestors.Add(current);
            current = current.Parent;
        }

        var path = new List<IWidget>();
        for (var i = ancestors.Count - 1; i >= 0; i--)
        {
            var widget = ancestors[i];
            if (widget is IEventHandler<T> or IEventObserver<T>)
                path.Add(widget);
        }

        return path;
    }

    static IWidget BuildRouteChain(int depth)
    {
        IWidget parent = new route_node();
        var current = parent;

        for (var i = 0; i < depth; i++)
        {
            var child = new route_node();
            if (current is TestWidgetBase baseWidget)
                baseWidget.Add(child);
            current = child;
        }

        return current;
    }

    static PerfStats Measure(int samples, Func<int, object> probe)
    {
        var ticks = new long[samples];
        for (var i = 0; i < samples; i++)
        {
            var started = Stopwatch.GetTimestamp();
            _ = probe(i);
            ticks[i] = Stopwatch.GetTimestamp() - started;
        }

        return PerfStats.FromTicks(ticks);
    }

    static void WriteScenario(string name, PerfStats stats)
    {
        Console.WriteLine($"PERF_SCENARIO {name} mean={stats.MeanMs:F4} p50={stats.P50Ms:F4} p95={stats.P95Ms:F4} p99={stats.P99Ms:F4} tm99={stats.Tm99Ms:F4}");
    }

    static void WriteDelta(string name, PerfStats baseline, PerfStats indexed)
    {
        var meanDelta = PercentDelta(baseline.MeanMs, indexed.MeanMs);
        var p95Delta = PercentDelta(baseline.P95Ms, indexed.P95Ms);
        var p99Delta = PercentDelta(baseline.P99Ms, indexed.P99Ms);
        var tm99Delta = PercentDelta(baseline.Tm99Ms, indexed.Tm99Ms);
        Console.WriteLine($"PERF_DELTA {name} mean={meanDelta:+0.00;-0.00;0.00}% p95={p95Delta:+0.00;-0.00;0.00}% p99={p99Delta:+0.00;-0.00;0.00}% tm99={tm99Delta:+0.00;-0.00;0.00}%");
    }

    static double PercentDelta(double baseline, double measured)
    {
        if (baseline <= 0) return 0;
        return ((measured - baseline) / baseline) * 100.0;
    }

    readonly record struct PerfStats(double MeanMs, double P50Ms, double P95Ms, double P99Ms, double Tm99Ms)
    {
        public static PerfStats FromTicks(long[] ticks)
        {
            var sorted = (long[])ticks.Clone();
            Array.Sort(sorted);

            var total = 0d;
            for (var i = 0; i < ticks.Length; i++)
                total += ticks[i];

            return new(ToMs(total / Math.Max(1, ticks.Length)),
                       ToMs(Percentile(sorted, 50)),
                       ToMs(Percentile(sorted, 95)),
                       ToMs(Percentile(sorted, 99)),
                       ToMs(TailMean(sorted, 99)));
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

            long sum = 0;
            for (var i = start; i < sorted.Length; i++)
                sum += sorted[i];

            return (double)sum / Math.Max(1, sorted.Length - start);
        }

        static double ToMs(double ticks) => ticks * 1000.0 / Stopwatch.Frequency;
    }

    sealed class grid_root(int width, int height) : TestWidgetBase
    {
        List<layout_node> _nodes = [];

        public override Size Measure(SizeConstraint constraint) => new(width, height);

        public void SetNodes(List<layout_node> nodes) => _nodes = nodes;

        public override void Arrange(Rect rect)
        {
            base.Arrange(rect);
            for (var i = 0; i < _nodes.Count; i++)
                ArrangeChild(_nodes[i], _nodes[i].Rect);
        }

        public override void Render(Canvas canvas)
        {
        }
    }

    sealed class layout_node(Rect rect) : TestWidgetBase, IFocusable, IEventHandler<OnMouseMove>
    {
        public Rect Rect { get; } = rect;

        public void Handle(IEventContext ctx, in OnMouseMove e)
        {
        }

        public override Size Measure(SizeConstraint constraint) => new(Rect.Width, Rect.Height);

        public override void Render(Canvas canvas)
        {
        }
    }

    sealed class route_node : TestWidgetBase, IEventHandler<OnMouseMove>, IEventObserver<OnMouseMove>
    {
        public void Handle(IEventContext ctx, in OnMouseMove e)
        {
        }

        public void Observe(IEventObserverContext ctx, in OnMouseMove e)
        {
        }

        public override void Render(Canvas canvas)
        {
        }
    }
}
