using Shouldly;
using Thoth.Widgets;
using Thoth.Widgets.Layout;

namespace Comptatata.Tests.App.Cli.UI.Thoth.layout.leaf_layouts;

static class LeafLayoutTestHelpers
{
    public static ILayoutCreator GetLayoutCreator(IWidgetWithLayout widget)
    {
        var creator = widget.GetLayoutCreator();
        creator.ShouldNotBeNull();
        return creator;
    }

    public static WidgetSizeRequest Measure(IWidgetWithLayout widget, SizeConstraint constraint)
    {
        var creator = GetLayoutCreator(widget);
        WidgetSizeRequest[] desires = [];
        return creator.Measure(widget, constraint, desires);
    }

    public static void AssertMeasureReturnsNonZeroSize(IWidgetWithLayout widget, SizeConstraint constraint)
    {
        var request = Measure(widget, constraint);
        request.Size.Width.ShouldBeGreaterThan(0);
        request.Size.Height.ShouldBeGreaterThan(0);
    }

    public static void AssertArrangeDoesNotThrow(IWidgetWithLayout widget, Rect rect)
    {
        var creator = GetLayoutCreator(widget);
        var actual = new WidgetSize(widget, creator, rect);

        Should.NotThrow(() =>
        {
            WidgetSize[] children = [];
            creator.Arrange(widget, actual, ReadOnlySpan<WidgetSizeRequest>.Empty, children);
        });
    }

    public static void AssertAcceptVisitsZero(IWidgetWithLayout widget)
    {
        var visitor = new CountingVisitor();
        widget.Accept(ref visitor);
        visitor.Count.ShouldBe(0);
    }

    public struct CountingVisitor : IVisitor
    {
        public int Count;

        public void Visit(IWidgetWithLayout child)
        {
            _ = child;
            Count++;
        }
    }
}
