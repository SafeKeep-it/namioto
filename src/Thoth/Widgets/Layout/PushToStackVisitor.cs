namespace Thoth.Widgets;

internal struct PushToStackVisitor(Stack<IWidget> stack) : IChildVisitor
{
    public bool Visit(INode child)
    {
        if (child is not IWidget widget) return true;

        stack.Push(widget);
        return true;
    }
}
