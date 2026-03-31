using Shouldly;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Tests.eventing.dispatch.utilities;
using Thoth.Widgets;

namespace Thoth.Tests.eventing.dispatch.event_not_handled;

public class builds_bubble_path_for_event_type : IAsyncLifetime
{
    List<IWidget> _path = null!;
    TestWidget _root = null!;
    CapturingWidget _capturer = null!;
    TestWidget _inertPanel = null!;
    HandlingWidget _handler = null!;
    HandlingWidget _target = null!;

    public Task InitializeAsync()
    {
        _root = new TestWidget { Parent = SentinelWidget.Instance };
        _capturer = new CapturingWidget { Parent = _root };
        _root.AddChild(_capturer);
        _inertPanel = new TestWidget { Parent = _capturer };
        _capturer.AddChild(_inertPanel);
        _handler = new HandlingWidget { Parent = _inertPanel };
        _inertPanel.AddChild(_handler);
        _target = new HandlingWidget { Parent = _handler };
        _handler.AddChild(_target);

        _path = new EventDispatcher().GetBubblePath<OnMouseClick>(_target);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void excludes_widgets_not_implementing_handle_interfaces() =>
        _path.ShouldNotContain(_inertPanel);

    [Fact]
    public void excludes_widgets_only_implementing_capture_interfaces() =>
        _path.ShouldNotContain(_capturer);

    [Fact]
    public void includes_handler() =>
        _path.ShouldContain(_handler);

    [Fact]
    public void includes_target() =>
        _path.ShouldContain(_target);

    [Fact]
    public void orders_target_to_root() =>
        _path.ShouldBe([_target, _handler]);
}
