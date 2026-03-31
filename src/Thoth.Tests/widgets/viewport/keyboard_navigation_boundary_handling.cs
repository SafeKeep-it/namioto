using Shouldly;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Rendering;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.components;

public class keyboard_navigation_boundary_handling
{
    [Fact]
    public void when_viewport_is_at_top_and_up_arrow_is_pressed_then_event_is_not_handled()
    {
        var viewport = SetupViewport(offsetY: 0);
        var dispatcher = new EventDispatcher();

        dispatcher.Dispatch(viewport, new KeyPressedInput(new('\0', ConsoleKey.UpArrow, false, false, false)));

        dispatcher.EventContext.IsHandled.ShouldBeFalse();
        viewport.OffsetY.ShouldBe(0);
    }

    [Fact]
    public void when_viewport_is_at_bottom_and_down_arrow_is_pressed_then_event_is_not_handled()
    {
        var viewport = SetupViewport(offsetY: 2);
        var dispatcher = new EventDispatcher();

        dispatcher.Dispatch(viewport, new KeyPressedInput(new('\0', ConsoleKey.DownArrow, false, false, false)));

        dispatcher.EventContext.IsHandled.ShouldBeFalse();
        viewport.OffsetY.ShouldBe(2);
    }

    [Fact]
    public void when_viewport_is_between_boundaries_and_down_arrow_is_pressed_then_event_is_handled()
    {
        var viewport = SetupViewport(offsetY: 1);
        var dispatcher = new EventDispatcher();

        dispatcher.Dispatch(viewport, new KeyPressedInput(new('\0', ConsoleKey.DownArrow, false, false, false)));

        dispatcher.EventContext.IsHandled.ShouldBeTrue();
        viewport.OffsetY.ShouldBe(2);
    }

    static Viewport SetupViewport(int offsetY)
    {
        var viewport = new Viewport { ScrollDirection = ScrollDirection.Vertical, OffsetY = offsetY };
        viewport.Content = new fixed_height_content_widget();
        var engine = new FrameRenderer(fullRender: false);
        engine.RenderFrame(viewport,
                           new UiContext(viewport),
                           10,
                           4,
                           new Dictionary<IWidget, InvalidationKind>());
        return viewport;
    }

    sealed class fixed_height_content_widget : TestWidgetBase
    {
        public override Size Measure(SizeConstraint constraint) => new(10, 6);

        public override void Arrange(Rect rect)
        {
            base.Arrange(new(0, 0, 10, 6));
        }

        public override void Render(Canvas canvas)
        {
        }
    }
}
