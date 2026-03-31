using Shouldly;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Tests.eventing.utilities;

namespace Thoth.Tests.eventing.raised;

public class event_raised_is_enqueued
{
    [Fact]
    public void capture_phase_raises_event()
    {
        var target = new CaptureOnlyRaiser();
        var dispatcher = new EventDispatcher();

        dispatcher.Dispatch(target, new OnMouseClick());

        dispatcher.QueueCount.ShouldBe(1);
        dispatcher.DispatchAll();
        target.FocusCount.ShouldBe(1);
    }

    [Fact]
    public void bubble_phase_raises_event()
    {
        var target = new BubbleOnlyRaiser();
        var dispatcher = new EventDispatcher();

        dispatcher.Dispatch(target, new OnMouseClick());

        dispatcher.QueueCount.ShouldBe(1);
        dispatcher.DispatchAll();
        target.FocusCount.ShouldBe(1);
    }

    [Fact]
    public void handled_event_observers_raise_events()
    {
        var root = new HandledRaiser { ShouldMarkHandled = true };
        var dispatcher = new EventDispatcher();

        dispatcher.Dispatch(root, new OnMouseClick());

        dispatcher.DispatchAll();
        root.FocusCount.ShouldBe(1);
        root.BlurCount.ShouldBe(1);
    }
}

file class CaptureOnlyRaiser : EventingTestWidgetBase, ICapture<OnMouseClick>, IEventHandler<OnFocus>
{
    public int FocusCount { get; private set; }

    void ICapture<OnMouseClick>.Capture(IEventContext context, in OnMouseClick @event) =>
        context.RaiseEvent(new OnFocus());

    void IEventHandler<OnFocus>.Handle(IEventContext context, in OnFocus @event) => FocusCount++;
}

file class BubbleOnlyRaiser : EventingTestWidgetBase, IEventHandler<OnMouseClick>, IEventHandler<OnFocus>
{
    public int FocusCount { get; private set; }

    void IEventHandler<OnFocus>.Handle(IEventContext context, in OnFocus @event) => FocusCount++;

    void IEventHandler<OnMouseClick>.Handle(IEventContext context, in OnMouseClick @event) =>
        context.RaiseEvent(new OnFocus());
}

file class HandledRaiser : EventingTestWidgetBase,
                           ICapture<OnMouseClick>,
                           IEventHandler<OnMouseClick>,
                           IEventObserver<OnMouseClick>,
                           IEventHandler<OnFocus>,
                           IEventHandler<OnBlur>
{
    public bool ShouldMarkHandled { get; init; }
    public int FocusCount { get; private set; }
    public int BlurCount { get; private set; }

    void ICapture<OnMouseClick>.Capture(IEventContext context, in OnMouseClick @event)
    {
        context.RaiseEvent(new OnFocus());
        if (ShouldMarkHandled) context.IsHandled = true;
    }

    void IEventObserver<OnMouseClick>.Observe(IEventObserverContext context, in OnMouseClick @event) =>
        context.RaiseEvent(new OnBlur());

    void IEventHandler<OnBlur>.Handle(IEventContext context, in OnBlur @event) => BlurCount++;

    void IEventHandler<OnFocus>.Handle(IEventContext context, in OnFocus @event) => FocusCount++;

    void IEventHandler<OnMouseClick>.Handle(IEventContext context, in OnMouseClick @event)
    {
        context.RaiseEvent(new OnFocus());
        if (ShouldMarkHandled) context.IsHandled = true;
    }
}
