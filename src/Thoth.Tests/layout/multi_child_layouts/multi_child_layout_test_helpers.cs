using global::Thoth.Widgets;
using global::Thoth.Widgets.Layout;

namespace Comptatata.Tests.App.Cli.UI.Thoth.layout.multi_child_layouts;

internal struct CountingVisitor : IVisitor
{
    public int Count { get; private set; }

    public void Visit(IWidgetWithLayout child)
    {
        _ = child;
        Count++;
    }
}

internal static class MultiChildLayoutTestHelpers
{
    public static WidgetSizeRequest Request(int width, int height)
    {
        var child = new TextBar();
        return new WidgetSizeRequest(child, child.GetLayoutCreator(), new Size(width, height));
    }

    public static WidgetSize Actual(IWidgetWithLayout widget, Rect rect)
    {
        return new WidgetSize(widget, widget.GetLayoutCreator(), rect);
    }

    public static int CountChildren(IWidgetWithLayout widget)
    {
        var visitor = new CountingVisitor();
        widget.Accept(ref visitor);
        return visitor.Count;
    }

    public static TextBlock TextBlock(string text)
    {
        return new TextBlock { Text = text };
    }
}
