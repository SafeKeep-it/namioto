using Shouldly;
using Thoth;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class no_maximum_height_allows_text_editor_growth
{
    readonly Size _measuredSize;

    public no_maximum_height_allows_text_editor_growth()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        var editor = new TextEditor();
        root.Add(editor);

        var screen = new AttentionManager(terminal, root, editor);
        for (var i = 0; i < 8; i++)
        {
            screen.HandleText("x");
            if (i < 7) screen.HandleKey(new('\r', ConsoleKey.Enter, true, false, false));
        }

        _measuredSize = editor.GetRenderer().Measure(new(16, 40));

        var buffer = new ScreenBuffer(16, 8);
        var context = new RenderContext(new(new Screen()));
        editor.GetScribe().Draw(new Canvas(buffer, new(0, 0, 16, 8), context));
        buffer.WriteTerminalSnapshotSvg("no_maximum_height_allows_text_editor_growth.eight_lines.svg");
        buffer.WriteLayoutDebugSvg(editor, 16, 8, "no_maximum_height_allows_text_editor_growth.eight_lines.svg");
    }

    [Fact]
    public void when_no_maximum_height_is_set_and_eight_lines_are_entered_then_editor_measures_to_eight_rows()
    {
        _measuredSize.Height.ShouldBe(8);
    }
}
