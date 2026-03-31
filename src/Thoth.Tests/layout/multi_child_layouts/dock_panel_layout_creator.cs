using Shouldly;
using global::Thoth.Widgets;
using global::Thoth.Widgets.Layout;
using LayoutWidgetSize = global::Thoth.Widgets.Layout.WidgetSize;
using LayoutWidgetSizeRequest = global::Thoth.Widgets.Layout.WidgetSizeRequest;

namespace Comptatata.Tests.App.Cli.UI.Thoth.layout.multi_child_layouts;

public class DockPanelLayoutCreator
{
    [Fact]
    public void GetLayoutCreatorReturnsNonNull()
    {
        var widget = new DockPanel();

        widget.GetLayoutCreator().ShouldNotBeNull();
    }

    [Fact]
    public void AcceptVisitsAllDocks()
    {
        var widget = new DockPanel();
        widget.Add(new Dock { Position = DockPosition.Top, Content = new TextBar() });
        widget.Add(new Dock { Position = DockPosition.Bottom, Content = new Toggle() });
        widget.Add(new Dock { Position = DockPosition.Fill, Content = new TextBar() });

        MultiChildLayoutTestHelpers.CountChildren(widget).ShouldBe(3);
    }

    [Fact]
    public void MeasureReturnsFullConstraintSize()
    {
        var widget = new DockPanel();
        var creator = widget.GetLayoutCreator();
        var desires = new[]
                      {
                          MultiChildLayoutTestHelpers.Request(4, 1),
                          MultiChildLayoutTestHelpers.Request(1, 1),
                          MultiChildLayoutTestHelpers.Request(9, 3)
                      };

        var measured = creator.Measure(widget, new SizeConstraint(25, 8), desires).Size;

        measured.ShouldBe(new Size(25, 8));
    }

    [Fact]
    public void ArrangeAssignsTopBottomAndFillRects()
    {
        var widget = new DockPanel();
        var top = new Dock { Position = DockPosition.Top, Content = new TextBar { LeftTitle = "top" } };
        var bottom = new Dock { Position = DockPosition.Bottom, Content = new Toggle() };
        var fill = new Dock { Position = DockPosition.Fill, Content = new TextBar { LeftTitle = "fill" } };
        widget.Add(top);
        widget.Add(bottom);
        widget.Add(fill);

        var creator = widget.GetLayoutCreator();
        var desires = new[]
                      {
                          MultiChildLayoutTestHelpers.Request(10, 1),
                          MultiChildLayoutTestHelpers.Request(10, 1),
                          MultiChildLayoutTestHelpers.Request(10, 3)
                      };
        var children = new LayoutWidgetSize[3];

        creator.Arrange(widget,
                        MultiChildLayoutTestHelpers.Actual(widget, new Rect(0, 0, 10, 5)),
                        desires,
                        children);

        children[0].Rect.ShouldBe(new Rect(0, 0, 10, 1));
        children[1].Rect.ShouldBe(new Rect(0, 4, 10, 1));
        children[2].Rect.ShouldBe(new Rect(0, 1, 10, 3));
    }
}
