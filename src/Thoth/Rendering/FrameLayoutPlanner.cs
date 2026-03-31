using Thoth.Widgets;

namespace Thoth.Rendering;

internal static class FrameLayoutPlanner
{
    public static FrameLayoutDecision Decide(bool fullRenderMode,
                                             bool bufferResized,
                                             bool frameWrapped,
                                             IReadOnlyDictionary<IWidget, InvalidationKind> invalidations,
                                             Dictionary<IWidget, InvalidationKind> renderInvalidationBuffer)
    {
        var requiresFullFrame = fullRenderMode || bufferResized || frameWrapped;
        if (requiresFullFrame) return new(requiresFullFrame, true, null);

        var renderInvalidations = ExpandInvalidationsForPaint(invalidations, renderInvalidationBuffer);
        return new(requiresFullFrame, false, renderInvalidations);
    }

    static IReadOnlyDictionary<IWidget, InvalidationKind> ExpandInvalidationsForPaint(
        IReadOnlyDictionary<IWidget, InvalidationKind> invalidations,
        Dictionary<IWidget, InvalidationKind> buffer)
    {
        if (buffer.Count > 0)
            buffer.Clear();

        if (invalidations.Count > 0)
            buffer.EnsureCapacity(invalidations.Count);

        foreach (var pair in invalidations)
        {
            Record(buffer, pair.Key, pair.Value);
            if ((pair.Value & InvalidationKind.Layout) != 0)
                RecordDescendantContentInvalidations(buffer, pair.Key);

            var ancestor = pair.Key.Parent;
            while (ancestor is not null && ancestor is not SentinelWidget)
            {
                Record(buffer, ancestor, InvalidationKind.Descendant);
                ancestor = ancestor.Parent;
            }
        }

        return buffer;
    }

    static void RecordDescendantContentInvalidations(Dictionary<IWidget, InvalidationKind> invalidations, IWidget root)
    {
        var stack = new Stack<IWidget>();
        var visitor = new PushToStackVisitor(stack);
        WidgetTraversalExtensions.VisitChildrenReverse(root, ref visitor);

        while (stack.Count > 0)
        {
            var widget = stack.Pop();
            Record(invalidations, widget, InvalidationKind.Content);

            visitor = new PushToStackVisitor(stack);
            WidgetTraversalExtensions.VisitChildrenReverse(widget, ref visitor);
        }
    }

    static void Record(Dictionary<IWidget, InvalidationKind> invalidations,
                       IWidget widget,
                       InvalidationKind kind)
    {
        if (invalidations.TryGetValue(widget, out var existing))
            invalidations[widget] = existing | kind;
        else
            invalidations[widget] = kind;
    }
}
