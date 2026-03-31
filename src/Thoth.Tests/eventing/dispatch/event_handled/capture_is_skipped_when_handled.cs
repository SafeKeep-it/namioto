using Shouldly;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Tests.eventing.dispatch.utilities;

namespace Thoth.Tests.eventing.dispatch.event_handled;

public class capture_is_skipped_when_handled : IAsyncLifetime
{
    CapturingWidget _child = null!;
    CapturedWidget _observer = null!;
    CapturingWidget _root = null!;

    public Task InitializeAsync()
    {
        _root = new() { ShouldMarkHandled = true };
        _child = new() { Parent = _root };
        _root.AddChild(_child);
        _observer = new() { Parent = _child };
        _child.AddChild(_observer);

        new EventDispatcher().Dispatch(_observer, new OnMouseClick());
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void root_capturer_is_called() => _root.Handled.ShouldBeTrue();

    [Fact]
    public void child_capturer_is_skipped() => _child.Handled.ShouldBeFalse();

    [Fact]
    public void observer_is_called() => _observer.Observed.ShouldBeTrue();
}
