using System.Text;
using Shouldly;
using Thoth.Rendering;
using Thoth.Terminal.Raw.Egress;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.canvas_rendering;

public class terminal_scribe_rle
{
    [Fact]
    public void emits_repeat_sequence_for_full_frame_repeated_ascii_runs()
    {
        var bytes = RenderRepeatedLineFullFrame(width: 12, glyph: '-');
        var text = Encoding.UTF8.GetString(bytes);

        text.ShouldContain("\u001b[11b");
    }

    [Fact]
    public void emits_repeat_sequence_for_partial_frame_repeated_ascii_runs()
    {
        var bytes = RenderRepeatedLinePartialFrame(width: 12, glyph: '-');
        var text = Encoding.UTF8.GetString(bytes);

        text.ShouldContain("\u001b[11b");
    }

    static byte[] RenderRepeatedLineFullFrame(int width, char glyph)
    {
        var terminal = new MockTerminal { WindowWidth = width, WindowHeight = 1 };
        var scribe = new TerminalScribe(terminal);
        var root = new Screen();
        var context = new RenderContext(new UiContext(root));
        var buffer = new ScreenBuffer(width, 1);
        var canvas = new Canvas(buffer, new(0, 0, width, 1), context, frameNumber: 1);
        var style = new Style(new Color(220, 220, 220), new Color(20, 20, 20));

        canvas.Fill(0, 0, width, 1, (Rune)glyph, style);
        scribe.Render(buffer, context, frameNumber: 1, renderFullFrame: true);

        return terminal.DrainWrittenBytes();
    }

    static byte[] RenderRepeatedLinePartialFrame(int width, char glyph)
    {
        var terminal = new MockTerminal { WindowWidth = width, WindowHeight = 1 };
        var scribe = new TerminalScribe(terminal);
        var root = new Screen();
        var context = new RenderContext(new UiContext(root));
        var buffer = new ScreenBuffer(width, 1);
        var style = new Style(new Color(220, 220, 220), new Color(20, 20, 20));

        var initial = new Canvas(buffer, new(0, 0, width, 1), context, frameNumber: 1);
        initial.Fill(0, 0, width, 1, (Rune)' ', style);
        scribe.Render(buffer, context, frameNumber: 1, renderFullFrame: true);
        terminal.DrainWrittenBytes();

        var changed = new Canvas(buffer, new(0, 0, width, 1), context, frameNumber: 2);
        changed.Fill(0, 0, width, 1, (Rune)glyph, style);
        scribe.Render(buffer, context, frameNumber: 2, renderFullFrame: false);

        return terminal.DrainWrittenBytes();
    }
}
