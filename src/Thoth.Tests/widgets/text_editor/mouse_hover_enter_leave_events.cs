using Shouldly;
using Thoth;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class mouse_hover_enter_leave_events
{
    readonly hover_widget _left;
    readonly hover_widget _right;

    public mouse_hover_enter_leave_events()
    {
        var terminal = new MockTerminal();
        var root = new split_root_widget();
        _left = new hover_widget();
        _right = new hover_widget();
        root.SetChildren(_left, _right);

        var screen = new AttentionManager(terminal, root, keyboardFocus: null);
        screen.Render();

        screen.HandleMouseMove(1, 0);
        screen.HandleMouseMove(2, 0);
        screen.HandleMouseMove(6, 0);
        screen.HandleMouseMove(20, 0);
    }

    [Fact]
    public void when_pointer_enters_left_then_left_receives_enter_once()
    {
        _left.EnterCount.ShouldBe(1);
    }

    [Fact]
    public void when_pointer_moves_within_left_then_no_duplicate_enter_is_emitted()
    {
        _left.EnterCount.ShouldBe(1);
    }

    [Fact]
    public void when_pointer_moves_from_left_to_right_then_left_receives_leave_and_right_receives_enter()
    {
        _left.LeaveCount.ShouldBe(1);
        _right.EnterCount.ShouldBe(1);
    }

    [Fact]
    public void when_pointer_leaves_root_then_current_hover_receives_leave()
    {
        _right.LeaveCount.ShouldBe(1);
    }

    sealed class split_root_widget : TestWidgetBase
    {
        hover_widget _left = null!;
        hover_widget _right = null!;

        public void SetChildren(hover_widget left, hover_widget right)
        {
            _left = left;
            _right = right;
            Add(left);
            Add(right);
        }

        public override void Arrange(Rect rect)
        {
            ArrangeChild(_left, new(0, 0, 5, 1));
            ArrangeChild(_right, new(5, 0, 5, 1));
        }

        public override void Render(Canvas canvas)
        {
        }
    }

    sealed class hover_widget : TestWidgetBase,
                                IEventHandler<OnMouseEnter>,
                                IEventHandler<OnMouseLeave>
    {
        public int EnterCount { get; private set; }
        public int LeaveCount { get; private set; }

        public void Handle(IEventContext ctx, in OnMouseEnter e)
        {
            EnterCount++;
        }

        public void Handle(IEventContext ctx, in OnMouseLeave e)
        {
            LeaveCount++;
        }

        public override void Render(Canvas canvas)
        {
        }
    }
}
