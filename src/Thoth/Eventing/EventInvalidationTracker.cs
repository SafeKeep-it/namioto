using Thoth.Eventing.Events;
using Thoth.Rendering;
using Thoth.Widgets;

namespace Thoth.Eventing;

internal class EventInvalidationTracker(EventContext context)
{
    public void InvalidateContent(IWidget target)
    {
        context.RecordInvalidation(target, InvalidationKind.Content);
    }

    public void InvalidateLayout(IWidget target)
    {
        context.RecordInvalidation(target, InvalidationKind.Layout);
    }

    public void Track<T>(IWidget target, in T @event) where T : struct
    {
        switch (@event)
        {
            case OnLayoutChanged:
                InvalidateLayout(target);
                break;
            case OnContentChanged:
                InvalidateContent(target);
                break;
        }
    }
}
