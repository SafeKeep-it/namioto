using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Tests.eventing.utilities;

namespace Thoth.Tests.eventing.dispatch.utilities;

public class HandlingWidget : EventingTestWidgetBase, IEventHandler<OnMouseClick>
{
    public bool Handled { get; private set; }
    public bool ShouldMarkHandled { get; set; }

    public void Handle(IEventContext context, in OnMouseClick @event)
    {
        Handled = true;
        if (ShouldMarkHandled) context.IsHandled = true;
    }
}
