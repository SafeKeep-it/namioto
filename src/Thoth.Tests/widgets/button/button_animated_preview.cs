using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Rendering;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Thoth.Tests.widgets.button;

public class button_animated_preview
{
    const int Width = 80;
    const int Height = 24;

    [Fact]
    public void button_hover_animation()
    {
        var root = new Screen();
        var button = new Button { Text = "Click Me" };
        root.Add(button);

        var uiContext = new UiContext(root);
        var dispatcher = new EventDispatcher();
        var frames = new List<terminal_snapshot_assertions.TrueColorInteractionFrame>();

        frames.Add(CaptureFrame(root, uiContext, 1500, null, null));

        dispatcher.Dispatch(button, new OnMouseEnter());
        frames.Add(CaptureFrame(root, uiContext, 2000, 5, 1));

        dispatcher.Dispatch(button, new OnMouseLeave());
        frames.Add(CaptureFrame(root, uiContext, 1500, null, null));

        terminal_snapshot_assertions.WriteTrueColorAnimatedSvg(
            "button_hover.animated.svg",
            frames,
            new RenderContext(uiContext));
    }

    static terminal_snapshot_assertions.TrueColorInteractionFrame CaptureFrame(
        IWidget root,
        UiContext uiContext,
        int durationMs,
        int? mouseX,
        int? mouseY)
    {
        var buffer = new ScreenBuffer(Width, Height);
        tree_render_harness.Render(root, buffer, uiContext);
        return new(JsonTerminal.Capture(buffer), durationMs, mouseX, mouseY);
    }
}
