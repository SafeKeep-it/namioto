namespace Thoth.Terminal.Raw.Ingress;

public enum ScreenOpKind : byte
{
    Add,
    Update,
    Delete,
    Append,
    Freeze,
    Start,
    Stop,
    StateChange,
    Key,
    EscapeKey,
    MouseMove,
    MouseScroll,
    MouseDown,
    MouseUp,
    Paste,
    Resize
}