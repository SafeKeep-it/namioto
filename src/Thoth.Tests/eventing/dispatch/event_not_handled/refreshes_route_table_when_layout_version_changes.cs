using Shouldly;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Rendering;
using Thoth.Tests.eventing.dispatch.utilities;
using Thoth.Widgets;

namespace Thoth.Tests.eventing.dispatch.event_not_handled;

public class refreshes_route_table_when_layout_version_changes : IAsyncLifetime
{
    EventDispatcher _dispatcher = null!;
    FrameLayoutState _layoutState = null!;
    TestWidget _root = null!;
    HandlingWidget _handlerA = null!;
    HandlingWidget _handlerB = null!;
    CapturingWidget _capturerA = null!;
    CapturingWidget _capturerB = null!;
    HandlingWidget _target = null!;

    public Task InitializeAsync()
    {
        _root = new TestWidget { Parent = SentinelWidget.Instance };
        _handlerA = new HandlingWidget { Parent = _root };
        _handlerB = new HandlingWidget { Parent = _root };
        _capturerA = new CapturingWidget { Parent = _root };
        _capturerB = new CapturingWidget { Parent = _root };
        _target = new HandlingWidget { Parent = _handlerA };

        _root.AddChild(_handlerA);
        _root.AddChild(_handlerB);
        _root.AddChild(_capturerA);
        _root.AddChild(_capturerB);
        _handlerA.AddChild(_target);

        _dispatcher = new EventDispatcher();
        _layoutState = new FrameLayoutState();
        _layoutState.BeginLayout();
        _dispatcher.SetLayoutState(_layoutState);

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void keeps_cached_bubble_path_when_layout_version_is_unchanged()
    {
        var first = _dispatcher.GetBubblePath<OnMouseClick>(_target);
        _target.Parent = _handlerB;

        var second = _dispatcher.GetBubblePath<OnMouseClick>(_target);

        second.ShouldBeSameAs(first);
        second.ShouldBe([_target, _handlerA]);
    }

    [Fact]
    public void rebuilds_bubble_path_after_layout_version_changes()
    {
        _ = _dispatcher.GetBubblePath<OnMouseClick>(_target);
        _target.Parent = _handlerB;
        _layoutState.BeginLayout();
        _dispatcher.SetLayoutState(_layoutState);

        var refreshed = _dispatcher.GetBubblePath<OnMouseClick>(_target);

        refreshed.ShouldBe([_target, _handlerB]);
    }

    [Fact]
    public void rebuilds_capture_path_after_layout_version_changes()
    {
        _target.Parent = _capturerA;
        var first = _dispatcher.GetCapturePath<OnMouseClick>(_target);
        first.ShouldBe([_capturerA]);

        _target.Parent = _capturerB;
        _layoutState.BeginLayout();
        _dispatcher.SetLayoutState(_layoutState);

        var refreshed = _dispatcher.GetCapturePath<OnMouseClick>(_target);

        refreshed.ShouldBe([_capturerB]);
    }
}
