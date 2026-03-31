using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Tests.eventing.utilities;

namespace Thoth.Tests.eventing.dispatch.utilities;

public class CapturedWidget : EventingTestWidgetBase, IEventObserver<OnMouseClick>
{
    public bool Observed { get; private set; }
    public void Observe(IEventObserverContext context, in OnMouseClick @event) => Observed = true;
}
