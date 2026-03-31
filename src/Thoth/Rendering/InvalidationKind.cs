namespace Thoth.Rendering;

[Flags]
public enum InvalidationKind
{
    None = 0,
    Content = 1,
    Layout = 2,
    Descendant = 4
}
