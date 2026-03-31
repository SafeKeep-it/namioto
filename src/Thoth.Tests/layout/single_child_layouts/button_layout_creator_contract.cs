using Shouldly;
using Thoth.Widgets;
using Thoth.Widgets.Layout;

namespace Comptatata.Tests.App.Cli.UI.Thoth.layout.single_child_layouts;

public class button_layout_creator_contract
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
        var chrome = single_child_layout_test_support.GetField<Border>(widget, "_chrome");
        var visitor = new counting_layout_child_visitor();

        widget.Accept(ref visitor);

        visitor.Count.ShouldBe(1);
        visitor.LastChild.ShouldBeSameAs(chrome);
    }

    [Fact]
    public void measure_accounts_for_child_desired_size()
    {
        var widget = create_widget();
        var chrome = single_child_layout_test_support.GetField<Border>(widget, "_chrome");
        var chromeContent = (IWidgetWithLayout)chrome.Content;
        var chromeContentDesired = single_child_layout_test_support.MeasureChild(chromeContent, new SizeConstraint(8, 4));
        var childDesired = chrome.GetLayoutCreator().Measure(chrome, new SizeConstraint(8, 4), [chromeContentDesired]);

        var result = widget.GetLayoutCreator().Measure(widget, new SizeConstraint(8, 4), [childDesired]);

        result.Size.ShouldBe(new Size(8, childDesired.Size.Height));
        result.Size.Width.ShouldBeGreaterThanOrEqualTo(childDesired.Size.Width);
        result.Size.Height.ShouldBeGreaterThanOrEqualTo(childDesired.Size.Height);
    }

    [Fact]
    public void arrange_writes_child_rect_into_span()
    {
        var widget = create_widget();
        var creator = widget.GetLayoutCreator();
        var children = new WidgetSize[1];

        creator.Arrange(widget, new WidgetSize(widget, creator, new Rect(0, 0, 8, 3)), ReadOnlySpan<WidgetSizeRequest>.Empty, children);

        children[0].Rect.ShouldBe(new Rect(0, 0, 8, 3));
    }

    static Button create_widget()
    {
        return new Button
               {
                   Text = "OK",
                   MinWidth = 8,
                   BorderStyle = BorderStyle.Inset
               };
    }
}
