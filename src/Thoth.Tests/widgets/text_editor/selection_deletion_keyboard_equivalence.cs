using Shouldly;
using Thoth;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class selection_deletion_keyboard_equivalence
{
    readonly TextEditor _deleteEditor;
    readonly TextEditor _backspaceEditor;

    public selection_deletion_keyboard_equivalence()
    {
        _deleteEditor = setup_with_key(ConsoleKey.Delete);
        _backspaceEditor = setup_with_key(ConsoleKey.Backspace);
    }

    [Fact]
    public void when_delete_or_backspace_is_used_on_selected_text_then_text_outcome_is_equivalent()
    {
        _deleteEditor.Text.ShouldBe(_backspaceEditor.Text);
    }

    [Fact]
    public void when_delete_or_backspace_is_used_on_selected_text_then_caret_outcome_is_equivalent()
    {
        _deleteEditor.CaretIndex.ShouldBe(_backspaceEditor.CaretIndex);
    }

    [Fact]
    public void when_delete_or_backspace_is_used_on_selected_text_then_selection_is_cleared_in_both_cases()
    {
        _deleteEditor.SelectionRange.ShouldBeNull();
        _backspaceEditor.SelectionRange.ShouldBeNull();
    }

    static TextEditor setup_with_key(ConsoleKey key)
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        var editor = new TextEditor();
        root.Add(editor);

        var screen = new AttentionManager(terminal, root, editor);
        screen.HandleText("hello world");
        screen.HandleKey(new('\0', ConsoleKey.LeftArrow, true, true, false));
        screen.HandleKey(new('\0', key, false, false, false));

        var suffix = key == ConsoleKey.Delete ? "delete" : "backspace";
        var buffer = new ScreenBuffer(16, 1);
        var context = new RenderContext(new(new Screen()));
        editor.GetScribe().Draw(new Canvas(buffer, new(0, 0, 16, 1), context));
        buffer.WriteTerminalSnapshotSvg($"selection_deletion_keyboard_equivalence.{suffix}.svg");
        buffer.WriteLayoutDebugSvg(editor, 16, 1, $"selection_deletion_keyboard_equivalence.{suffix}.svg");

        return editor;
    }
}
