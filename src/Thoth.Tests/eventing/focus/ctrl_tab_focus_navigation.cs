using Shouldly;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Navigation.Focus;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.focus_navigation;

public class ctrl_tab_focus_navigation
{
    [Fact]
    public void ctrl_tab_moves_focus_to_nearest_focusable_by_rect_distance()
    {
        var terminal = new MockTerminal();
        var current = new focusable_widget();
        var nearest = new focusable_widget();
        var farther = new focusable_widget();

        var layoutRoot = new focus_layout_root_widget((current, new Rect(0, 0, 2, 2)),
                                                      (nearest, new Rect(3, 0, 2, 2)),
                                                      (farther, new Rect(12, 0, 2, 2)));

        var attention = new AttentionManager(terminal, layoutRoot, current);
        attention.Render();

        attention.HandleKey(new('\t', ConsoleKey.Tab, shift: false, alt: false, control: true));

        attention.KeyboardFocus.ShouldBe(nearest);
    }

    [Fact]
    public void ctrl_tab_breaks_tied_distance_by_reverse_draw_order()
    {
        var terminal = new MockTerminal();
        var current = new focusable_widget();
        var lowZ = new focusable_widget();
        var highZ = new focusable_widget();

        var layoutRoot = new focus_layout_root_widget((current, new Rect(10, 10, 2, 2)),
                                                      (lowZ, new Rect(0, 10, 2, 2)),
                                                      (highZ, new Rect(20, 10, 2, 2)));

        var attention = new AttentionManager(terminal, layoutRoot, current);
        attention.Render();

        attention.HandleKey(new('\t', ConsoleKey.Tab, shift: false, alt: false, control: true));

        attention.KeyboardFocus.ShouldBe(highZ);
    }

    sealed class focusable_widget : TestWidgetBase, IFocusable, IEventHandler<OnFocus>
    {
        public void Handle(IEventContext ctx, in OnFocus e)
        {
            ctx.IsHandled = true;
        }

        public override void Render(Canvas canvas)
        {
        }
    }

    sealed class focus_layout_root_widget : TestWidgetBase
    {
        readonly (IWidget Widget, Rect Rect) _first;
        readonly (IWidget Widget, Rect Rect) _second;
        readonly (IWidget Widget, Rect Rect) _third;

        public focus_layout_root_widget((IWidget Widget, Rect Rect) first,
                                        (IWidget Widget, Rect Rect) second,
                                        (IWidget Widget, Rect Rect) third)
        {
            _first = first;
            _second = second;
            _third = third;

            Add(first.Widget);
            Add(second.Widget);
            Add(third.Widget);
        }

        public override void Arrange(Rect rect)
        {
            base.Arrange(rect);
            ArrangeChild(_first.Widget, _first.Rect);
            ArrangeChild(_second.Widget, _second.Rect);
            ArrangeChild(_third.Widget, _third.Rect);
        }

        public override void Render(Canvas canvas)
        {
        }
    }
}
