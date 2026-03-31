using Shouldly;
using global::Thoth.Widgets;
using global::Thoth.Widgets.Layout;
using LayoutWidgetSize = global::Thoth.Widgets.Layout.WidgetSize;
using LayoutWidgetSizeRequest = global::Thoth.Widgets.Layout.WidgetSizeRequest;

namespace Comptatata.Tests.App.Cli.UI.Thoth.layout.multi_child_layouts;

public class OverlayWidgetLayoutCreator
{
    [Fact]
    public void GetLayoutCreatorReturnsNonNull()
    {
        var widget = new OverlayWidget { Content = new TextBar() };

        widget.GetLayoutCreator().ShouldNotBeNull();
    }

    [Fact]
    public void AcceptVisitsContentBackdropAndActiveOverlay()
    {
        var widget = new OverlayWidget { Content = new TextBar() };
        widget.Show(MultiChildLayoutTestHelpers.TextBlock("overlay"));

        MultiChildLayoutTestHelpers.CountChildren(widget).ShouldBe(3);
    }

    [Fact]
    public void MeasureReturnsFullConstraintSize()
    {
        var widget = new OverlayWidget { Content = new TextBar() };
        widget.Show(MultiChildLayoutTestHelpers.TextBlock("overlay"));

        var measured = widget.GetLayoutCreator().Measure(widget,
                                                         new SizeConstraint(30, 8),
                                                         Span<LayoutWidgetSizeRequest>.Empty).Size;

        measured.ShouldBe(new Size(30, 8));
    }

    [Fact]
    public void ArrangeAssignsFullBoundsToVisibleChildren()
    {
        var widget = new OverlayWidget { Content = new TextBar() };
        widget.Show(MultiChildLayoutTestHelpers.TextBlock("overlay"));

        var creator = widget.GetLayoutCreator();
        var children = new LayoutWidgetSize[3];

        creator.Arrange(widget,
                        MultiChildLayoutTestHelpers.Actual(widget, new Rect(0, 0, 20, 6)),
                        ReadOnlySpan<LayoutWidgetSizeRequest>.Empty,
                        children);

        children[0].Rect.ShouldBe(new Rect(0, 0, 20, 6));
        children[1].Rect.ShouldBe(new Rect(0, 0, 20, 6));
        children[2].Rect.ShouldBe(new Rect(0, 0, 20, 6));
    }
}
