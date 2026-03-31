using Shouldly;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Tests.eventing.dispatch.utilities;
using Thoth.Widgets;

namespace Thoth.Tests.eventing.dispatch.event_handled;

public class bubble_is_skipped_when_handled : IAsyncLifetime
{
    ObserveHandledWidget _root = null!;
    HandlingWidget _child = null!;
    HandlingWidget _target = null!;

    public Task InitializeAsync()
    {
        _root = new ObserveHandledWidget();
        _child = new HandlingWidget { Parent = _root };
        _root.AddChild(_child);
        _target = new HandlingWidget { Parent = _child, ShouldMarkHandled = true };
        _child.AddChild(_target);

        new EventDispatcher().Dispatch(_target, new OnMouseClick());
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void target_handler_is_called() => _target.Handled.ShouldBeTrue();

    [Fact]
    public void child_handler_is_skipped() => _child.Handled.ShouldBeFalse();

    [Fact]
    public void root_observer_is_called() => _root.Observed.ShouldBeTrue();
}
