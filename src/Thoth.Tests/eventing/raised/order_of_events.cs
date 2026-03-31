using Shouldly;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Tests.eventing.raised.utilities;

namespace Thoth.Tests.eventing.raised;

public class order_of_events
{
    [Fact]
    public void input_events_processed_before_raised_events()
    {
        var widget = new OrderTrackingWidget();
        var dispatcher = new EventDispatcher();

        dispatcher.Enqueue(widget, new OnMouseClick());
        dispatcher.Enqueue(widget, new OnMouseEnter());

        dispatcher.DispatchAll();

        widget.HandledEvents.ShouldBe(["OnMouseClick", "OnMouseEnter", "OnFocus"]);
        dispatcher.QueueCount.ShouldBe(0);
    }
}