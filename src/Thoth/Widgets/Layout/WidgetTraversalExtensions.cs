using System.Buffers;

namespace Thoth.Widgets;

public static class WidgetTraversalExtensions
{
    public static void VisitChildrenReverse<TVisitor>(IWidget widget, ref TVisitor visitor)
        where TVisitor : struct, IChildVisitor
    {
        var initialChildren = ArrayPool<INode>.Shared.Rent(8);
        var collect = new CollectChildrenVisitor(initialChildren);
        widget.VisitChildren(ref collect);

        var children = collect.Children;
        var count = collect.Count;
        try
        {
            for (var i = count - 1; i >= 0; i--)
            {
                if (!visitor.Visit(children[i]))
                    break;
            }
        }
        finally
        {
            Array.Clear(children, 0, count);
            ArrayPool<INode>.Shared.Return(children, clearArray: false);
        }
    }
}
