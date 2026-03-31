namespace Thoth.Widgets;

[Flags]
public enum ScrollDirection
{
    None = 0,
    Horizontal = 1,
    Vertical = 2,
    Both = Horizontal | Vertical
}