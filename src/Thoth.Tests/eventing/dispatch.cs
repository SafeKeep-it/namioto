using Shouldly;
using Thoth.Eventing;
using Thoth.Rendering;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.event_system;

public readonly struct TestEvent(List<string> log)
{
    public List<string> Log { get; } = log;
}

public class CaptureWidget(string name, bool marksHandled) : TestWidgetBase, ICapture<TestEvent>
{
    public void Capture(IEventContext ctx, in TestEvent e)
    {
        e.Log.Add($"Capture:{name}");
        if (marksHandled) ctx.IsHandled = true;
    }

    public override void Render(Canvas canvas) { }
}

public class HandlerWidget(string name, bool marksHandled) : TestWidgetBase, IEventHandler<TestEvent>
{
    public void Handle(IEventContext ctx, in TestEvent e)
    {
        e.Log.Add($"Handle:{name}");
        if (marksHandled) ctx.IsHandled = true;
    }

    public override void Render(Canvas canvas) { }
}

public readonly struct MouseTestEvent(int x, int y, List<string> log) : IMouseEvent
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public List<string> Log { get; } = log;
}

public class HandlerMouseWidget(string name, Rect rect, bool marksHandled)
    : TestWidgetBase, IEventHandler<MouseTestEvent>
{
    public void Handle(IEventContext ctx, in MouseTestEvent e)
    {
        e.Log.Add($"Handle:{name}");
        if (marksHandled) ctx.IsHandled = true;
    }

    public override Size Measure(SizeConstraint constraint) => new(rect.Width, rect.Height);

    public override void Arrange(Rect r)
    {
        base.Arrange(rect);
    }

    public override void Render(Canvas canvas) { }
}

public class event_dispatcher_dispatch : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void dispatch_targets_mouse_event_by_hit_test()
    {
        var root = new HandlerMouseWidget("root", new(0, 0, 10, 10), false);
        var child = new HandlerMouseWidget("child", new(2, 2, 5, 5), true);
        var dispatcher = new EventDispatcher();
        root.Add(child);

        var layoutState = new FrameLayoutState();
        layoutState.BeginLayout();
        layoutState.Set(root, new(0, 0, 10, 10), 0);
        layoutState.Set(child, new(2, 2, 5, 5), 1);

        var log1 = new List<string>();
        var e1 = new MouseTestEvent(3, 3, log1);
        var t1 = layoutState.WidgetAt(e1.X, e1.Y);
        if (!ReferenceEquals(t1, SentinelWidget.Instance)) dispatcher.Dispatch(t1, e1);
        dispatcher.DispatchAll();
        log1.ShouldBe(new[] { "Handle:child" });

        var log2 = new List<string>();
        var e2 = new MouseTestEvent(1, 1, log2);
        var t2 = layoutState.WidgetAt(e2.X, e2.Y);
        if (!ReferenceEquals(t2, SentinelWidget.Instance)) dispatcher.Dispatch(t2, e2);
        dispatcher.DispatchAll();
        log2.ShouldBe(new[] { "Handle:root" });

        var log3 = new List<string>();
        var e3 = new MouseTestEvent(11, 11, log3);
        var t3 = layoutState.WidgetAt(e3.X, e3.Y);
        if (!ReferenceEquals(t3, SentinelWidget.Instance)) dispatcher.Dispatch(t3, e3);
        dispatcher.DispatchAll();
        log3.ShouldBeEmpty();
    }

    [Fact]
    public void dispatch_runs_capture_then_bubble_handlers()
    {
        var root = new CaptureWidget("root", false);
        var middle = new HandlerWidget("middle", false);
        var focused = new HandlerWidget("focused", true);

        root.Add(middle);
        middle.Add(focused);

        var dispatcher = new EventDispatcher();
        var log = new List<string>();
        var e = new TestEvent(log);
        dispatcher.Dispatch(focused, e);
        dispatcher.DispatchAll();

        log.ShouldBe(new[] { "Capture:root", "Handle:focused" });
    }

    [Fact]
    public void handled_event_stops_parent_handler_bubble()
    {
        var root = new HandlerWidget("root", false);
        var focused = new HandlerWidget("focused", true);
        var dispatcher = new EventDispatcher();

        root.Add(focused);

        var log = new List<string>();
        var e = new TestEvent(log);
        dispatcher.Dispatch(focused, e);
        dispatcher.DispatchAll();

        log.ShouldBe(new[] { "Handle:focused" });
    }

    [Fact]
    public void handled_event_skips_parent_even_when_parent_would_handle()
    {
        var root = new HandlerWidget("root", true);
        var focused = new HandlerWidget("focused", true);
        var dispatcher = new EventDispatcher();

        root.Add(focused);

        var log = new List<string>();
        var e = new TestEvent(log);
        dispatcher.Dispatch(focused, e);
        dispatcher.DispatchAll();

        log.ShouldBe(new[] { "Handle:focused" });
    }

    [Fact]
    public void capture_handler_can_stop_bubble_handlers()
    {
        var root = new CaptureWidget("root", true);
        var focused = new HandlerWidget("focused", true);
        var dispatcher = new EventDispatcher();

        root.Add(focused);

        var log = new List<string>();
        var e = new TestEvent(log);
        dispatcher.Dispatch(focused, e);
        dispatcher.DispatchAll();

        log.ShouldBe(new[] { "Capture:root" });
    }
}
