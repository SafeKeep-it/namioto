using Shouldly;
using global::Thoth.Widgets;
using global::Thoth.Widgets.Layout;
using LayoutWidgetSize = global::Thoth.Widgets.Layout.WidgetSize;
using LayoutWidgetSizeRequest = global::Thoth.Widgets.Layout.WidgetSizeRequest;
using WidgetAlign = global::Thoth.Widgets.Layout.Align;
using single_child_layout_test_support = Comptatata.Tests.App.Cli.UI.Thoth.layout.single_child_layouts.single_child_layout_test_support;

namespace Comptatata.Tests.App.Cli.UI.Thoth.layout.multi_child_layouts;

public class AlignLayoutCreator
{
    [Fact]
    public void GetLayoutCreatorReturnsNonNull()
    {
        var widget = new WidgetAlign();

        widget.GetLayoutCreator().ShouldNotBeNull();
    }

    [Fact]
    public void AcceptVisitsContent()
    {
        var widget = new WidgetAlign { Content = MultiChildLayoutTestHelpers.TextBlock("AB") };

        MultiChildLayoutTestHelpers.CountChildren(widget).ShouldBe(1);
    }

    [Fact]
    public void MeasureReturnsChildSizeInContentMode()
    {
        var widget = new WidgetAlign
                     {
                         WidthSizeMode = WidthSizeMode.Content,
                         Content = MultiChildLayoutTestHelpers.TextBlock("AB")
                     };

        var measured = widget.GetLayoutCreator().Measure(widget,
                                                          new SizeConstraint(10, 3),
                                                          ReadOnlySpan<LayoutWidgetSizeRequest>.Empty).Size;

        measured.ShouldBe(new Size(2, 1));
    }

    [Fact]
    public void ArrangePositionsContentUsingHorizontalAlignment()
    {
        var widget = new WidgetAlign
                     {
                         WidthSizeMode = WidthSizeMode.Content,
                         HorizontalAlignment = HorizontalAlignment.Center,
                         Content = MultiChildLayoutTestHelpers.TextBlock("AB")
                     };

        var creator = widget.GetLayoutCreator();
        var child = (IWidgetWithLayout)widget.Content;
        var childDesired = single_child_layout_test_support.MeasureChild(child, new SizeConstraint(10, 3));
        var children = new LayoutWidgetSize[1];
        var desires = new[] { childDesired };

        creator.Arrange(widget,
                        MultiChildLayoutTestHelpers.Actual(widget, new Rect(0, 0, 10, 3)),
                        desires,
                        children);

        children[0].Rect.ShouldBe(new Rect(4, 0, 2, 1));
    }
}
