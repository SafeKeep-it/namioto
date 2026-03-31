using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Rendering;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Thoth.Tests.widgets.border;

public class border_animated_preview
{
    const int Width = 80;
    const int Height = 24;
    const int CursorX = 40;
    const int CursorY = 12;

    [Fact]
    public void border_hover_animation()
    {
        var root = new Screen();
        var border = new Border
        {
            BorderStyle = BorderStyle.Rounded,
            Content = new TextBlock
            {
                Text = "Hover over the border to see the highlight effect.",
                Overflow = TextOverflow.Wrap
            }
        };
        root.Add(border);

        var uiContext = new UiContext(root);
        var dispatcher = new EventDispatcher();
        var frames = new List<terminal_snapshot_assertions.TrueColorInteractionFrame>();

        frames.Add(CaptureFrame(root, uiContext, 1500, null, null));

        dispatcher.Dispatch(border, new OnMouseEnter());
        frames.Add(CaptureFrame(root, uiContext, 2000, CursorX, CursorY));

        dispatcher.Dispatch(border, new OnMouseLeave());
        frames.Add(CaptureFrame(root, uiContext, 1500, null, null));

        terminal_snapshot_assertions.WriteTrueColorAnimatedSvg(
            "border_hover.animated.svg",
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
