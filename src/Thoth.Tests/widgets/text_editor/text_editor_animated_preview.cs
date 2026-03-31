using Thoth.Eventing;
using Thoth.Rendering;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Thoth.Tests.widgets.text_editor;

public class text_editor_animated_preview
{
    const int Width = 80;
    const int Height = 24;
    const string TypingText = "Hello, Thoth!";
    const int CharDurationMs = 150;
    const int PauseDurationMs = 1500;

    [Fact]
    public void text_editor_typing_animation()
    {
        var root = new Screen();
        var editor = new TextEditor();
        root.Add(editor);

        var uiContext = new UiContext(root);
        var dispatcher = new EventDispatcher();
        var frames = new List<terminal_snapshot_assertions.TrueColorInteractionFrame>();

        frames.Add(CaptureFrame(root, uiContext, PauseDurationMs));

        foreach (var ch in TypingText)
        {
            dispatcher.Dispatch(editor, new TextInput(ch.ToString()));
            frames.Add(CaptureFrame(root, uiContext, CharDurationMs));
        }

        frames[^1] = frames[^1] with { DurationMs = PauseDurationMs };

        terminal_snapshot_assertions.WriteTrueColorAnimatedSvg(
            "text_editor_typing.animated.svg",
            frames,
            new RenderContext(uiContext));
    }

    static terminal_snapshot_assertions.TrueColorInteractionFrame CaptureFrame(
        IWidget root,
        UiContext uiContext,
        int durationMs)
    {
        var buffer = new ScreenBuffer(Width, Height);
        tree_render_harness.Render(root, buffer, uiContext);
        return new(JsonTerminal.Capture(buffer), durationMs);
    }
}
