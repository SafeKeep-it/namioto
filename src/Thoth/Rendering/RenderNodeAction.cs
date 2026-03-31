namespace Thoth.Rendering;

[Flags]
public enum RenderNodeAction
{
    None = 0,
    VisitNode = 1,
    DrawSelf = 2
}
