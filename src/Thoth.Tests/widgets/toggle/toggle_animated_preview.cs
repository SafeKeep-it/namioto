using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Rendering;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Thoth.Tests.widgets.toggle;

public class toggle_animated_preview
{
    const int Width = 80;
    const int Height = 24;

    [Fact]
    public void toggle_click_animation()
    {
        var root = new Screen();
        var toggle = new Toggle { IsChecked = false };
        root.Add(toggle);

        var uiContext = new UiContext(root);
        var dispatcher = new EventDispatcher();
        var frames = new List<terminal_snapshot_assertions.TrueColorInteractionFrame>();

        frames.Add(CaptureFrame(root, uiContext, 1500, 1, 0));

        dispatcher.Dispatch(toggle, new OnMouseClick());
        frames.Add(CaptureFrame(root, uiContext, 2000, 1, 0));

        dispatcher.Dispatch(toggle, new OnMouseClick());
        frames.Add(CaptureFrame(root, uiContext, 1500, 1, 0));

        terminal_snapshot_assertions.WriteTrueColorAnimatedSvg(
            "toggle_click.animated.svg",
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
