using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Tests.eventing.utilities;

namespace Thoth.Tests.eventing.raised.utilities;

public class OrderTrackingWidget : EventingTestWidgetBase,
                                   IEventHandler<OnMouseClick>,
                                   IEventHandler<OnMouseEnter>,
                                   IEventHandler<OnFocus>
{
    public List<string> HandledEvents { get; } = [];

    public void Handle(IEventContext context, in OnMouseClick @event)
    {
        HandledEvents.Add("OnMouseClick");
        context.RaiseEvent(new OnFocus());
    }

    public void Handle(IEventContext context, in OnMouseEnter @event)
    {
        HandledEvents.Add("OnMouseEnter");
    }

    public void Handle(IEventContext context, in OnFocus @event)
    {
        HandledEvents.Add("OnFocus");
    }
}
