using Shouldly;
using Thoth.Widgets;
using Thoth.Widgets.Layout;

namespace Comptatata.Tests.App.Cli.UI.Thoth.layout.single_child_layouts;

public class modal_dialog_layout_creator_contract
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
        var chrome = single_child_layout_test_support.GetField<Border>(widget, "Chrome");
        var visitor = new counting_layout_child_visitor();

        widget.Accept(ref visitor);

        visitor.Count.ShouldBe(1);
        visitor.LastChild.ShouldBeSameAs(chrome);
    }

    [Fact]
    public void measure_accounts_for_child_desired_size()
    {
        var widget = create_widget();
        var chrome = single_child_layout_test_support.GetField<Border>(widget, "Chrome");
        var chromeContent = (IWidgetWithLayout)chrome.Content;
        var chromeContentDesired = single_child_layout_test_support.MeasureChild(chromeContent, new SizeConstraint(18, 6));
        var childDesired = chrome.GetLayoutCreator().Measure(chrome, new SizeConstraint(18, 6), [chromeContentDesired]);

        var result = widget.GetLayoutCreator().Measure(widget, new SizeConstraint(40, 20), [childDesired]);

        result.Size.ShouldBe(new Size(20, 8));
        result.Size.Width.ShouldBeGreaterThanOrEqualTo(childDesired.Size.Width);
        result.Size.Height.ShouldBeGreaterThanOrEqualTo(childDesired.Size.Height);
    }

    [Fact]
    public void arrange_writes_child_rect_into_span()
    {
        var widget = create_widget();
        var creator = widget.GetLayoutCreator();
        var children = new WidgetSize[1];

        creator.Arrange(widget, new WidgetSize(widget, creator, new Rect(0, 0, 40, 20)), ReadOnlySpan<WidgetSizeRequest>.Empty, children);

        children[0].Rect.ShouldBe(new Rect(10, 6, 20, 8));
    }

    static ModalDialog create_widget()
    {
        var widget = new ModalDialog
                     {
                         Width = 20,
                         Height = 8,
                         MaxWidthRatio = 1.0,
                         MaxHeightRatio = 1.0,
                         Title = "Title",
                         Content = new Viewport
                                   {
                                       Content = new TextBlock
                                                 {
                                                     Text = "Body",
                                                     Overflow = TextOverflow.Wrap
                                                 }
                                   }
                     };
        widget.FooterButtons.Add(new Button { Text = "OK" });
        return widget;
    }
}
