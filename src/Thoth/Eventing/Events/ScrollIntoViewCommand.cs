using Thoth.Rendering;
using Thoth.Widgets;

namespace Thoth.Eventing.Events;

public readonly struct ScrollIntoViewCommand(Rect region, IWidget sender)
{
    public Rect Region { get; } = region;
    public IWidget Sender { get; } = sender;
}