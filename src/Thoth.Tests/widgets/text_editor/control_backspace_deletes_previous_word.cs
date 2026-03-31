using Shouldly;
using Thoth;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class control_backspace_deletes_previous_word
{
    readonly TextEditor _editor;

    public control_backspace_deletes_previous_word()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        _editor = new TextEditor();
        root.Add(_editor);

        var screen = new AttentionManager(terminal, root, _editor);
        screen.HandleText("hello world");
        screen.HandleKey(new('\0', ConsoleKey.Backspace, false, false, true));

        var buffer = new ScreenBuffer(16, 1);
        var context = new RenderContext(new(new Screen()));
        _editor.GetScribe().Draw(new Canvas(buffer, new(0, 0, 16, 1), context));
        buffer.WriteTerminalSnapshotSvg("control_backspace_deletes_previous_word.delete_word.svg");
        buffer.WriteLayoutDebugSvg(_editor, 16, 1, "control_backspace_deletes_previous_word.delete_word.svg");
    }

    [Fact]
    public void when_control_backspace_is_pressed_at_end_of_text_then_previous_word_is_deleted()
    {
        _editor.Text.ShouldBe("hello ");
    }

    [Fact]
    public void when_control_backspace_deletes_previous_word_then_caret_moves_to_word_boundary()
    {
        _editor.CaretIndex.ShouldBe(6);
    }
}
