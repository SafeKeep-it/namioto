using Shouldly;
using Thoth.Widgets;
using Thoth.Widgets.Layout;

namespace Comptatata.Tests.App.Cli.UI.Thoth.layout.single_child_layouts;

public class border_layout_creator_contract
{
    [Fact]
    public void get_layout_creator_returns_non_null()
    {
        create_widget().GetLayoutCreator().ShouldNotBeNull();
    }

    [Fact]
    public void enumerate_children_visits_exactly_one_child()
    {
        var widget = create_widget();
        var visitor = new counting_layout_child_visitor();

        widget.Accept(ref visitor);

        visitor.Count.ShouldBe(1);
        visitor.LastChild.ShouldBeSameAs(widget.Content);
    }

    [Fact]
    public void measure_accounts_for_child_desired_size()
    {
        var widget = create_widget();
        var child = (IWidgetWithLayout)widget.Content;
        var childDesired = single_child_layout_test_support.MeasureChild(child, new SizeConstraint(6, 4));

        var result = widget.GetLayoutCreator().Measure(widget, new SizeConstraint(8, 6), [childDesired]);

        result.Size.ShouldBe(new Size(childDesired.Size.Width + 2, childDesired.Size.Height + 2));
        result.Size.Width.ShouldBeGreaterThanOrEqualTo(childDesired.Size.Width);
        result.Size.Height.ShouldBeGreaterThanOrEqualTo(childDesired.Size.Height);
    }

    [Fact]
    public void arrange_writes_child_rect_into_span()
    {
        var widget = create_widget();
        var creator = widget.GetLayoutCreator();
        var children = new WidgetSize[1];

        creator.Arrange(widget, new WidgetSize(widget, creator, new Rect(0, 0, 9, 5)), ReadOnlySpan<WidgetSizeRequest>.Empty, children);

        children[0].Rect.ShouldBe(new Rect(1, 1, 7, 3));
    }

    static Border create_widget()
    {
        return new Border
               {
                   BorderStyle = BorderStyle.Single,
                   Content = new TextBlock
                             {
                                 Text = "child",
                                 Overflow = TextOverflow.Wrap
                             }
               };
    }
}
