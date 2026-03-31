using Shouldly;
using global::Thoth.Widgets;
using global::Thoth.Widgets.Layout;
using LayoutWidgetSize = global::Thoth.Widgets.Layout.WidgetSize;
using LayoutWidgetSizeRequest = global::Thoth.Widgets.Layout.WidgetSizeRequest;

namespace Comptatata.Tests.App.Cli.UI.Thoth.layout.multi_child_layouts;

public class ButtonGroupLayoutCreator
{
    [Fact]
    public void GetLayoutCreatorReturnsNonNull()
    {
        var widget = new ButtonGroup();

        widget.GetLayoutCreator().ShouldNotBeNull();
    }

    [Fact]
    public void AcceptVisitsAllButtons()
    {
        var widget = new ButtonGroup();
        widget.Add(new Button { Text = "One" });
        widget.Add(new Button { Text = "Two" });
        widget.Add(new Button { Text = "Three" });

        MultiChildLayoutTestHelpers.CountChildren(widget).ShouldBe(3);
    }

    [Fact]
    public void MeasureSumsWidthsAndUsesTallestChild()
    {
        var widget = new ButtonGroup { ButtonGap = 2 };
        var creator = widget.GetLayoutCreator();
        var desires = new[]
                      {
                          MultiChildLayoutTestHelpers.Request(5, 3),
                          MultiChildLayoutTestHelpers.Request(4, 2),
                          MultiChildLayoutTestHelpers.Request(6, 4)
                      };

        var measured = creator.Measure(widget, new SizeConstraint(30, 10), desires).Size;

        measured.ShouldBe(new Size(19, 4));
    }

    [Fact]
    public void ArrangePlacesButtonsLeftToRightWithGap()
    {
        var widget = new ButtonGroup { ButtonGap = 1 };
        var one = new Button { Text = "One" };
        var two = new Button { Text = "Two" };
        widget.Add(one);
        widget.Add(two);

        var creator = widget.GetLayoutCreator();
        var desires = new[]
                      {
                          MultiChildLayoutTestHelpers.Request(5, 3),
                          MultiChildLayoutTestHelpers.Request(5, 3)
                      };
        var children = new LayoutWidgetSize[2];

        creator.Arrange(widget,
                        MultiChildLayoutTestHelpers.Actual(widget, new Rect(0, 0, 20, 3)),
                        desires,
                        children);

        children[0].Rect.ShouldBe(new Rect(0, 0, 5, 3));
        children[1].Rect.ShouldBe(new Rect(6, 0, 5, 3));
    }
}
