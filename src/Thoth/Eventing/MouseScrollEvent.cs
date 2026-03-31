namespace Thoth.Eventing;

public readonly struct MouseScrollEvent(int x, int y, int delta) : IMouseEvent
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public int Delta { get; } = delta;
}