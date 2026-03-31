using Shouldly;
using global::Thoth.Widgets;
using global::Thoth.Widgets.Layout;
using LayoutWidgetSize = global::Thoth.Widgets.Layout.WidgetSize;
using LayoutWidgetSizeRequest = global::Thoth.Widgets.Layout.WidgetSizeRequest;

namespace Comptatata.Tests.App.Cli.UI.Thoth.layout.multi_child_layouts;

public class ScreenLayoutCreator
{
    [Fact]
    public void GetLayoutCreatorReturnsNonNull()
    {
        var widget = new Screen();

        widget.GetLayoutCreator().ShouldNotBeNull();
    }

    [Fact]
    public void AcceptVisitsAllChildren()
    {
        var widget = new Screen();
        widget.Add(new TextBar());
        widget.Add(new TextBar());

        MultiChildLayoutTestHelpers.CountChildren(widget).ShouldBe(2);
    }

    [Fact]
    public void MeasureReturnsFullConstraintSize()
    {
        var widget = new Screen();
        var creator = widget.GetLayoutCreator();
        var desires = new[]
                      {
                          MultiChildLayoutTestHelpers.Request(3, 1),
                          MultiChildLayoutTestHelpers.Request(5, 2)
                      };

        var measured = creator.Measure(widget, new SizeConstraint(30, 9), desires).Size;

        measured.ShouldBe(new Size(30, 9));
    }

    [Fact]
    public void ArrangeAssignsFullBoundsToEachChild()
    {
        var widget = new Screen();
        widget.Add(new TextBar());
        widget.Add(new TextBar());

        var creator = widget.GetLayoutCreator();
        var children = new LayoutWidgetSize[2];

        creator.Arrange(widget,
                        MultiChildLayoutTestHelpers.Actual(widget, new Rect(0, 0, 20, 6)),
                        ReadOnlySpan<LayoutWidgetSizeRequest>.Empty,
                        children);

        children[0].Rect.ShouldBe(new Rect(0, 0, 20, 6));
        children[1].Rect.ShouldBe(new Rect(0, 0, 20, 6));
    }
}
