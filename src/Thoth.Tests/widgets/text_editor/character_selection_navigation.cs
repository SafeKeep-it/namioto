using Shouldly;
using Thoth;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class character_selection_navigation
{
    readonly TextEditor _leftEditor;
    readonly TextEditor _rightEditor;

    public character_selection_navigation()
    {
        _leftEditor = SetupWithShiftLeftTwice();
        _rightEditor = SetupWithShiftRightTwice();
    }

    [Fact]
    public void when_shift_left_is_pressed_repeatedly_then_selection_extends_left_one_character_at_a_time()
    {
        _leftEditor.SelectionRange.ShouldBe((3, 5));
        _leftEditor.CaretIndex.ShouldBe(3);
    }

    [Fact]
    public void when_shift_right_is_pressed_repeatedly_then_selection_extends_right_one_character_at_a_time()
    {
        _rightEditor.SelectionRange.ShouldBe((0, 2));
        _rightEditor.CaretIndex.ShouldBe(2);
    }

    static TextEditor SetupWithShiftLeftTwice()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        var editor = new TextEditor();
        root.Add(editor);

        var screen = new AttentionManager(terminal, root, editor);
        screen.HandleText("hello");
        screen.HandleKey(new('\0', ConsoleKey.LeftArrow, true, false, false));
        screen.HandleKey(new('\0', ConsoleKey.LeftArrow, true, false, false));

        var buffer = new ScreenBuffer(10, 1);
        var context = new RenderContext(new(new Screen()));
        editor.GetScribe().Draw(new Canvas(buffer, new(0, 0, 10, 1), context));
        buffer.WriteTerminalSnapshotSvg("character_selection_navigation.shift_left.svg");
        buffer.WriteLayoutDebugSvg(editor, 10, 1, "character_selection_navigation.shift_left.svg");

        return editor;
    }

    static TextEditor SetupWithShiftRightTwice()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        var editor = new TextEditor();
        root.Add(editor);

        var screen = new AttentionManager(terminal, root, editor);
        screen.HandleText("hello");
        screen.HandleKey(new('\0', ConsoleKey.Home, false, false, false));
        screen.HandleKey(new('\0', ConsoleKey.RightArrow, true, false, false));
        screen.HandleKey(new('\0', ConsoleKey.RightArrow, true, false, false));

        var buffer = new ScreenBuffer(10, 1);
        var context = new RenderContext(new(new Screen()));
        editor.GetScribe().Draw(new Canvas(buffer, new(0, 0, 10, 1), context));
        buffer.WriteTerminalSnapshotSvg("character_selection_navigation.shift_right.svg");
        buffer.WriteLayoutDebugSvg(editor, 10, 1, "character_selection_navigation.shift_right.svg");

        return editor;
    }
}
