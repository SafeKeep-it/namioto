using Shouldly;
using global::Thoth.Widgets;
using global::Thoth.Widgets.Layout;
using LayoutWidgetSize = global::Thoth.Widgets.Layout.WidgetSize;
using LayoutWidgetSizeRequest = global::Thoth.Widgets.Layout.WidgetSizeRequest;

namespace Comptatata.Tests.App.Cli.UI.Thoth.layout.multi_child_layouts;

public class StackPanelLayoutCreator
{
    [Fact]
    public void GetLayoutCreatorReturnsNonNull()
    {
        var widget = new StackPanel();

        widget.GetLayoutCreator().ShouldNotBeNull();
    }

    [Fact]
    public void AcceptVisitsAllItems()
    {
        var widget = new StackPanel();
        widget.Items.Add(new TextBar());
        widget.Items.Add(new TextBar());
        widget.Items.Add(new TextBar());

        MultiChildLayoutTestHelpers.CountChildren(widget).ShouldBe(3);
    }

    [Fact]
    public void MeasureSumsChildHeightsAndUsesConstraintWidth()
    {
        var widget = new StackPanel();
        var creator = widget.GetLayoutCreator();
        var desires = new[]
                      {
                          MultiChildLayoutTestHelpers.Request(3, 2),
                          MultiChildLayoutTestHelpers.Request(7, 4),
                          MultiChildLayoutTestHelpers.Request(5, 1)
                      };

        var measured = creator.Measure(widget, new SizeConstraint(20, 50), desires).Size;

        measured.ShouldBe(new Size(20, 7));
    }

    [Fact]
    public void ArrangeStacksChildrenFromBottom()
    {
        var widget = new StackPanel();
        widget.Items.Add(new TextBar { LeftTitle = "one" });
        widget.Items.Add(new TextBar { LeftTitle = "two" });
        widget.Items.Add(new TextBar { LeftTitle = "three" });

        var creator = widget.GetLayoutCreator();
        var desires = new[]
                      {
                          MultiChildLayoutTestHelpers.Request(12, 1),
                          MultiChildLayoutTestHelpers.Request(12, 1),
                          MultiChildLayoutTestHelpers.Request(12, 1)
                      };
        var children = new LayoutWidgetSize[3];

        creator.Arrange(widget,
                        MultiChildLayoutTestHelpers.Actual(widget, new Rect(0, 0, 12, 5)),
                        desires,
                        children);

        children[0].Rect.ShouldBe(new Rect(0, 2, 12, 1));
        children[1].Rect.ShouldBe(new Rect(0, 3, 12, 1));
        children[2].Rect.ShouldBe(new Rect(0, 4, 12, 1));
    }
}
