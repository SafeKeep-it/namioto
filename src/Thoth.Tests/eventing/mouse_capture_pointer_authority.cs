using Shouldly;
using Comptatata.Tests.App.Cli;
using Thoth;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Tests.utilities;

namespace Thoth.Tests.eventing;

public class mouse_capture_pointer_authority
{
    [Fact]
    public void while_mouse_is_captured_all_pointer_events_route_to_capture_owner()
    {
        var terminal = new MockTerminal();
        var root = new split_root_widget();
        var captureOwner = new capture_widget();
        var sibling = new pointer_probe_widget();
        root.SetChildren(captureOwner, sibling);

        var screen = new AttentionManager(terminal, root, keyboardFocus: null);
        screen.Render();

        screen.HandleMouseDown(1, 0, MouseButton.Left);
        screen.HandleMouseMove(6, 0);
        screen.HandleScroll(6, 0, -1);
        screen.HandleMouseDown(6, 0, MouseButton.Left);
        screen.HandleMouseUp(6, 0, MouseButton.Left);

        captureOwner.DownCount.ShouldBe(2);
        captureOwner.MoveCount.ShouldBe(1);
        captureOwner.ScrollCount.ShouldBe(1);
        captureOwner.UpCount.ShouldBe(1);
        captureOwner.ClickCount.ShouldBe(1);

        sibling.DownCount.ShouldBe(0);
        sibling.MoveCount.ShouldBe(0);
        sibling.ScrollCount.ShouldBe(0);
        sibling.UpCount.ShouldBe(0);
        sibling.ClickCount.ShouldBe(0);
    }

    sealed class split_root_widget : TestWidgetBase
    {
        pointer_probe_widget _left = null!;
        pointer_probe_widget _right = null!;

        public void SetChildren(pointer_probe_widget left, pointer_probe_widget right)
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

    class pointer_probe_widget : TestWidgetBase,
                                IEventHandler<OnMouseDown>,
                                IEventHandler<OnMouseMove>,
                                IEventHandler<OnMouseUp>,
                                IEventHandler<OnMouseClick>,
                                IEventHandler<MouseScrollEvent>
    {
        public int DownCount { get; private set; }
        public int MoveCount { get; private set; }
        public int UpCount { get; private set; }
        public int ClickCount { get; private set; }
        public int ScrollCount { get; private set; }

        public virtual void Handle(IEventContext context, in OnMouseDown e)
        {
            _ = context;
            _ = e;
            DownCount++;
        }

        public void Handle(IEventContext context, in OnMouseMove e)
        {
            _ = context;
            _ = e;
            MoveCount++;
        }

        public virtual void Handle(IEventContext context, in OnMouseUp e)
        {
            _ = context;
            _ = e;
            UpCount++;
        }

        public void Handle(IEventContext context, in OnMouseClick e)
        {
            _ = context;
            _ = e;
            ClickCount++;
        }

        public void Handle(IEventContext context, in MouseScrollEvent e)
        {
            _ = context;
            _ = e;
            ScrollCount++;
        }

        public override void Render(Canvas canvas)
        {
        }
    }

    sealed class capture_widget : pointer_probe_widget
    {
        public override void Handle(IEventContext context, in OnMouseDown e)
        {
            base.Handle(context, e);
            context.CaptureMouse();
        }

        public override void Handle(IEventContext context, in OnMouseUp e)
        {
            base.Handle(context, e);
            context.ReleaseMouse();
        }
    }
}
