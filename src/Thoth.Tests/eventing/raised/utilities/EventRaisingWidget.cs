using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Tests.eventing.utilities;

namespace Thoth.Tests.eventing.raised.utilities;

public class EventRaisingWidget : EventingTestWidgetBase,
                                  ICapture<OnMouseClick>,
                                  IEventHandler<OnMouseClick>,
                                  IEventObserver<OnMouseClick>
{
    public void Capture(IEventContext context, in OnMouseClick @event)
    {
        context.RaiseEvent(new OnFocus());
    }

    public void Handle(IEventContext context, in OnMouseClick @event)
    {
        context.RaiseEvent(new OnFocus());
    }

    public void Observe(IEventObserverContext context, in OnMouseClick @event)
    {
        context.RaiseEvent(new OnBlur());
    }
}
