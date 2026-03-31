using Shouldly;
using Thoth.Modal;
using Thoth.Widgets;
using Thoth.Widgets.Layout;

namespace Comptatata.Tests.App.Cli.UI.Thoth.layout.single_child_layouts;

public class single_choice_list_layout_creator_contract
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
        var table = single_child_layout_test_support.GetField<Table>(widget, "Table");
        var visitor = new counting_layout_child_visitor();

        widget.Accept(ref visitor);

        visitor.Count.ShouldBe(1);
        visitor.LastChild.ShouldBeSameAs(table);
    }

    [Fact]
    public void measure_accounts_for_child_desired_size()
    {
        var widget = create_widget();
        var table = single_child_layout_test_support.GetField<Table>(widget, "Table");
        var childDesired = single_child_layout_test_support.MeasureChild(table, new SizeConstraint(20, 8));

        var result = widget.GetLayoutCreator().Measure(widget, new SizeConstraint(20, 8), [childDesired]);

        result.Size.ShouldBe(childDesired.Size);
        result.Size.Width.ShouldBeGreaterThanOrEqualTo(childDesired.Size.Width);
        result.Size.Height.ShouldBeGreaterThanOrEqualTo(childDesired.Size.Height);
    }

    [Fact]
    public void arrange_writes_child_rect_into_span()
    {
        var widget = create_widget();
        var creator = widget.GetLayoutCreator();
        var children = new WidgetSize[1];

        creator.Arrange(widget, new WidgetSize(widget, creator, new Rect(0, 0, 20, 6)), ReadOnlySpan<WidgetSizeRequest>.Empty, children);

        children[0].Rect.ShouldBe(new Rect(0, 0, 20, 6));
    }

    static SingleChoiceList create_widget()
    {
        var widget = new SingleChoiceList();
        widget.SetChoices([
            new ModalDialogChoice("choice-a", "Alpha", true),
            new ModalDialogChoice("choice-b", "Beta")
        ]);
        return widget;
    }
}
