using Shouldly;
using Thoth;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class vertical_selection_and_control_delete
{
    [Fact]
    public void when_shift_down_is_pressed_then_selection_extends_to_same_column_on_next_line()
    {
        var editor = create_editor_with_text("one two\nthree four");
        var screen = create_screen(editor);

        screen.HandleKey(new('\0', ConsoleKey.Home, false, false, false));
        screen.HandleKey(new('\0', ConsoleKey.DownArrow, true, false, false));

        editor.SelectionRange.ShouldBe((0, 8));
        editor.CaretIndex.ShouldBe(8);

        write_snapshot(editor, "vertical_selection_and_control_delete.shift_down.svg");
    }

    [Fact]
    public void when_shift_up_is_pressed_then_selection_extends_to_same_column_on_previous_line()
    {
        var editor = create_editor_with_text("one two\nthree four");
        var screen = create_screen(editor);

        screen.HandleKey(new('\0', ConsoleKey.Home, false, false, false));
        screen.HandleKey(new('\0', ConsoleKey.DownArrow, false, false, false));
        screen.HandleKey(new('\0', ConsoleKey.UpArrow, true, false, false));

        editor.SelectionRange.ShouldBe((0, 8));
        editor.CaretIndex.ShouldBe(0);

        write_snapshot(editor, "vertical_selection_and_control_delete.shift_up.svg");
    }

    [Fact]
    public void when_control_delete_is_pressed_then_next_word_boundary_segment_is_deleted()
    {
        var editor = create_editor_with_text("hello   world test");
        var screen = create_screen(editor);

        screen.HandleKey(new('\0', ConsoleKey.Home, false, false, false));
        screen.HandleKey(new('\0', ConsoleKey.Delete, false, false, true));

        editor.Text.ShouldBe("   world test");
        editor.CaretIndex.ShouldBe(0);
        editor.SelectionRange.ShouldBeNull();

        write_snapshot(editor, "vertical_selection_and_control_delete.control_delete.svg");
    }

    static TextEditor create_editor_with_text(string text)
    {
        var editor = new TextEditor();
        editor.Text = text;
        return editor;
    }

    static AttentionManager create_screen(TextEditor editor)
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        root.Add(editor);
        return new AttentionManager(terminal, root, editor);
    }

    static void write_snapshot(TextEditor editor, string name)
    {
        var buffer = new ScreenBuffer(20, 4);
        var context = new RenderContext(new(new Screen()));
        editor.GetScribe().Draw(new Canvas(buffer, new(0, 0, 20, 4), context));
        buffer.WriteTerminalSnapshotSvg(name);
        buffer.WriteLayoutDebugSvg(editor, 20, 4, name);
    }
}
