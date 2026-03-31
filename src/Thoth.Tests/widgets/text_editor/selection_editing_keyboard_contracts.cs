using Shouldly;
using Thoth;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class selection_editing_keyboard_contracts
{
    readonly TextEditor _deleteSelectionEditor;
    readonly TextEditor _shiftHomeEditor;
    readonly TextEditor _shiftEndEditor;

    public selection_editing_keyboard_contracts()
    {
        _deleteSelectionEditor = setup_delete_selection_case();
        _shiftHomeEditor = setup_shift_home_case();
        _shiftEndEditor = setup_shift_end_case();
    }

    [Fact]
    public void when_delete_is_pressed_with_selected_text_then_selection_is_deleted()
    {
        _deleteSelectionEditor.Text.ShouldBe("hello ");
        _deleteSelectionEditor.CaretIndex.ShouldBe(6);
        _deleteSelectionEditor.SelectionRange.ShouldBeNull();
    }

    [Fact]
    public void when_shift_home_is_pressed_then_selection_extends_to_start_of_text()
    {
        _shiftHomeEditor.SelectionRange.ShouldBe((0, 11));
        _shiftHomeEditor.CaretIndex.ShouldBe(0);
    }

    [Fact]
    public void when_shift_end_is_pressed_then_selection_extends_to_end_of_text()
    {
        _shiftEndEditor.SelectionRange.ShouldBe((0, 11));
        _shiftEndEditor.CaretIndex.ShouldBe(11);
    }

    static TextEditor setup_delete_selection_case()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        var editor = new TextEditor();
        root.Add(editor);

        var screen = new AttentionManager(terminal, root, editor);
        screen.HandleText("hello world");
        screen.HandleKey(new('\0', ConsoleKey.LeftArrow, true, true, false));
        screen.HandleKey(new('\0', ConsoleKey.Delete, false, false, false));

        var buffer = new ScreenBuffer(16, 1);
        var context = new RenderContext(new(new Screen()));
        editor.GetScribe().Draw(new Canvas(buffer, new(0, 0, 16, 1), context));
        buffer.WriteTerminalSnapshotSvg("selection_editing_keyboard_contracts.delete_selection.svg");
        buffer.WriteLayoutDebugSvg(editor, 16, 1, "selection_editing_keyboard_contracts.delete_selection.svg");

        return editor;
    }

    static TextEditor setup_shift_home_case()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        var editor = new TextEditor();
        root.Add(editor);

        var screen = new AttentionManager(terminal, root, editor);
        screen.HandleText("hello world");
        screen.HandleKey(new('\0', ConsoleKey.Home, true, false, false));

        var buffer = new ScreenBuffer(16, 1);
        var context = new RenderContext(new(new Screen()));
        editor.GetScribe().Draw(new Canvas(buffer, new(0, 0, 16, 1), context));
        buffer.WriteTerminalSnapshotSvg("selection_editing_keyboard_contracts.shift_home.svg");
        buffer.WriteLayoutDebugSvg(editor, 16, 1, "selection_editing_keyboard_contracts.shift_home.svg");

        return editor;
    }

    static TextEditor setup_shift_end_case()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        var editor = new TextEditor();
        root.Add(editor);

        var screen = new AttentionManager(terminal, root, editor);
        screen.HandleText("hello world");
        screen.HandleKey(new('\0', ConsoleKey.Home, false, false, false));
        screen.HandleKey(new('\0', ConsoleKey.End, true, false, false));

        var buffer = new ScreenBuffer(16, 1);
        var context = new RenderContext(new(new Screen()));
        editor.GetScribe().Draw(new Canvas(buffer, new(0, 0, 16, 1), context));
        buffer.WriteTerminalSnapshotSvg("selection_editing_keyboard_contracts.shift_end.svg");
        buffer.WriteLayoutDebugSvg(editor, 16, 1, "selection_editing_keyboard_contracts.shift_end.svg");

        return editor;
    }
}
