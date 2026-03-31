using Shouldly;
using global::Thoth.Widgets;
using global::Thoth.Widgets.Layout;
using LayoutWidgetSize = global::Thoth.Widgets.Layout.WidgetSize;
using LayoutWidgetSizeRequest = global::Thoth.Widgets.Layout.WidgetSizeRequest;

namespace Comptatata.Tests.App.Cli.UI.Thoth.layout.multi_child_layouts;

public class TableLayoutCreator
{
    [Fact]
    public void GetLayoutCreatorReturnsNonNull()
    {
        var widget = new Table();

        widget.GetLayoutCreator().ShouldNotBeNull();
    }

    [Fact]
    public void AcceptVisitsAllCells()
    {
        var widget = new Table();
        widget.AddColumn(1);
        widget.AddColumn(1);
        widget.AddRow(MultiChildLayoutTestHelpers.TextBlock("AA"), MultiChildLayoutTestHelpers.TextBlock("BB"));
        widget.AddRow(MultiChildLayoutTestHelpers.TextBlock("CC"), MultiChildLayoutTestHelpers.TextBlock("DD"));

        MultiChildLayoutTestHelpers.CountChildren(widget).ShouldBe(4);
    }

    [Fact]
    public void MeasureReturnsConstraintWidthAndTotalRowHeight()
    {
        var widget = new Table();
        widget.AddColumn(1);
        widget.AddColumn(1);
        widget.AddRow(MultiChildLayoutTestHelpers.TextBlock("AA"), MultiChildLayoutTestHelpers.TextBlock("BB"));
        widget.AddRow(MultiChildLayoutTestHelpers.TextBlock("CC"), MultiChildLayoutTestHelpers.TextBlock("DD"));

        var measured = widget.GetLayoutCreator().Measure(widget,
                                                         new SizeConstraint(20, 6),
                                                         Span<LayoutWidgetSizeRequest>.Empty).Size;

        measured.ShouldBe(new Size(20, 2));
    }

    [Fact]
    public void ArrangeDistributesCellsByRowAndColumn()
    {
        var widget = new Table();
        var a = MultiChildLayoutTestHelpers.TextBlock("AA");
        var b = MultiChildLayoutTestHelpers.TextBlock("BB");
        var c = MultiChildLayoutTestHelpers.TextBlock("CC");
        var d = MultiChildLayoutTestHelpers.TextBlock("DD");
        widget.AddColumn(1);
        widget.AddColumn(1);
        widget.AddRow(a, b);
        widget.AddRow(c, d);

        var creator = widget.GetLayoutCreator();
        var children = new LayoutWidgetSize[4];

        creator.Arrange(widget,
                        MultiChildLayoutTestHelpers.Actual(widget, new Rect(0, 0, 20, 4)),
                        ReadOnlySpan<LayoutWidgetSizeRequest>.Empty,
                        children);

        children[0].Rect.ShouldBe(new Rect(0, 0, 10, 1));
        children[1].Rect.ShouldBe(new Rect(10, 0, 10, 1));
        children[2].Rect.ShouldBe(new Rect(0, 1, 10, 1));
        children[3].Rect.ShouldBe(new Rect(10, 1, 10, 1));
    }
}
