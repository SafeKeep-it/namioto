using Thoth.Eventing;

namespace Thoth.Eventing.Events;

public readonly struct OnMouseUp(int x = 0, int y = 0, MouseButton button = MouseButton.Left)
    : IMouseEvent
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public MouseButton Button { get; } = button;
}
