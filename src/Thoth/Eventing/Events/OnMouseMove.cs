using Thoth.Eventing;

namespace Thoth.Eventing.Events;

public readonly struct OnMouseMove(int x = 0, int y = 0) : IMouseEvent
{
    public int X { get; } = x;
    public int Y { get; } = y;
}
