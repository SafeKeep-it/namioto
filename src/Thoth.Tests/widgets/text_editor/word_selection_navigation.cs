using Shouldly;
using Thoth;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class word_selection_navigation
{
    readonly TextEditor _leftEditor;
    readonly TextEditor _rightEditor;
    readonly TextEditor _selectAllEditor;

    public word_selection_navigation()
    {
        _leftEditor = SetupWithShiftAltLeft();
        _rightEditor = SetupWithShiftAltRight();
        _selectAllEditor = SetupWithControlA();
    }

    [Fact]
    public void when_shift_alt_left_is_pressed_then_previous_word_is_selected()
    {
        _leftEditor.SelectionRange.ShouldBe((6, 11));
        _leftEditor.CaretIndex.ShouldBe(6);
    }

    [Fact]
    public void when_shift_alt_right_is_pressed_then_next_word_is_selected()
    {
        _rightEditor.SelectionRange.ShouldBe((0, 5));
        _rightEditor.CaretIndex.ShouldBe(5);
    }

    [Fact]
    public void when_control_a_is_pressed_then_all_text_is_selected()
    {
        _selectAllEditor.SelectionRange.ShouldBe((0, 11));
        _selectAllEditor.CaretIndex.ShouldBe(11);
    }

    static TextEditor SetupWithShiftAltLeft()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        var editor = new TextEditor();
        root.Add(editor);

        var screen = new AttentionManager(terminal, root, editor);
        screen.HandleText("hello world");
        screen.HandleKey(new('\0', ConsoleKey.LeftArrow, true, true, false));

        var buffer = new ScreenBuffer(16, 1);
        var context = new RenderContext(new(new Screen()));
        editor.GetScribe().Draw(new Canvas(buffer, new(0, 0, 16, 1), context));
        buffer.WriteTerminalSnapshotSvg("word_selection_navigation.shift_alt_left.svg");
        buffer.WriteLayoutDebugSvg(editor, 16, 1, "word_selection_navigation.shift_alt_left.svg");

        return editor;
    }

    static TextEditor SetupWithShiftAltRight()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        var editor = new TextEditor();
        root.Add(editor);

        var screen = new AttentionManager(terminal, root, editor);
        screen.HandleText("hello world");
        screen.HandleKey(new('\0', ConsoleKey.Home, false, false, false));
        screen.HandleKey(new('\0', ConsoleKey.RightArrow, true, true, false));

        var buffer = new ScreenBuffer(16, 1);
        var context = new RenderContext(new(new Screen()));
        editor.GetScribe().Draw(new Canvas(buffer, new(0, 0, 16, 1), context));
        buffer.WriteTerminalSnapshotSvg("word_selection_navigation.shift_alt_right.svg");
        buffer.WriteLayoutDebugSvg(editor, 16, 1, "word_selection_navigation.shift_alt_right.svg");

        return editor;
    }

    static TextEditor SetupWithControlA()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        var editor = new TextEditor();
        root.Add(editor);

        var screen = new AttentionManager(terminal, root, editor);
        screen.HandleText("hello world");
        screen.HandleKey(new('\0', ConsoleKey.A, false, false, true));

        var buffer = new ScreenBuffer(16, 1);
        var context = new RenderContext(new(new Screen()));
        editor.GetScribe().Draw(new Canvas(buffer, new(0, 0, 16, 1), context));
        buffer.WriteTerminalSnapshotSvg("word_selection_navigation.control_a.svg");
        buffer.WriteLayoutDebugSvg(editor, 16, 1, "word_selection_navigation.control_a.svg");

        return editor;
    }
}
