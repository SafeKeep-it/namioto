using Shouldly;
using Thoth;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class mouse_click_event_routing
{
    readonly click_widget _left;
    readonly click_widget _right;

    public mouse_click_event_routing()
    {
        var terminal = new MockTerminal();
        var root = new split_root_widget();
        _left = new();
        _right = new();
        root.SetChildren(_left, _right);

        var screen = new AttentionManager(terminal, root, keyboardFocus: null);
        screen.Render();
        screen.HandleMouseDown(1, 0, MouseButton.Left);
        screen.HandleMouseUp(1, 0, MouseButton.Left);
        screen.HandleMouseDown(1, 0, MouseButton.Left);
        screen.HandleMouseUp(6, 0, MouseButton.Left);
    }

    [Fact]
    public void when_mouse_down_and_up_happen_on_same_target_then_click_event_is_dispatched_once()
    {
        _left.ClickCount.ShouldBe(1);
    }

    [Fact]
    public void when_mouse_up_happens_on_different_target_then_click_event_is_not_dispatched()
    {
        _right.ClickCount.ShouldBe(0);
    }

    sealed class split_root_widget : TestWidgetBase
    {
        click_widget _left = null!;
        click_widget _right = null!;

        public void SetChildren(click_widget left, click_widget right)
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

    sealed class click_widget : TestWidgetBase,
                                IEventHandler<OnMouseClick>
    {
        public int ClickCount { get; private set; }

        public void Handle(IEventContext context, in OnMouseClick @event)
        {
            ClickCount++;
        }

        public override void Render(Canvas canvas)
        {
        }
    }
}
