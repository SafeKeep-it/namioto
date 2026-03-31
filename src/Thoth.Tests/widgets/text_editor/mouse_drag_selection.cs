using Shouldly;
using Thoth;
using Thoth.Eventing;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class mouse_drag_selection
{
    [Fact]
    public void when_mouse_drag_moves_right_then_selection_extends_to_drag_position()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        var editor = new TextEditor();
        root.Add(editor);
        root.GetRenderer().Arrange(new(0, 0, 20, 2));

        var screen = new AttentionManager(terminal, root, editor);
        screen.HandleText("hello world");

        screen.HandleMouseDown(1, 0, MouseButton.Left);
        screen.HandleMouseMove(6, 0);
        screen.HandleMouseUp(6, 0, MouseButton.Left);

        editor.SelectionRange.ShouldBe((1, 6));
        editor.CaretIndex.ShouldBe(6);

        var buffer = new ScreenBuffer(20, 2);
        var context = new RenderContext(new(new Screen()));
        editor.GetScribe().Draw(new Canvas(buffer, new(0, 0, 20, 2), context));
        buffer.WriteTerminalSnapshotSvg("mouse_drag_selection.rightward_drag.svg");
        buffer.WriteLayoutDebugSvg(editor, 20, 2, "mouse_drag_selection.rightward_drag.svg");
    }

    [Fact]
    public void when_mouse_drag_moves_left_then_selection_extends_backwards()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        var editor = new TextEditor();
        root.Add(editor);
        root.GetRenderer().Arrange(new(0, 0, 20, 2));

        var screen = new AttentionManager(terminal, root, editor);
        screen.HandleText("hello world");

        screen.HandleMouseDown(8, 0, MouseButton.Left);
        screen.HandleMouseMove(3, 0);
        screen.HandleMouseUp(3, 0, MouseButton.Left);

        editor.SelectionRange.ShouldBe((3, 8));
        editor.CaretIndex.ShouldBe(3);

        var buffer = new ScreenBuffer(20, 2);
        var context = new RenderContext(new(new Screen()));
        editor.GetScribe().Draw(new Canvas(buffer, new(0, 0, 20, 2), context));
        buffer.WriteTerminalSnapshotSvg("mouse_drag_selection.leftward_drag.svg");
        buffer.WriteLayoutDebugSvg(editor, 20, 2, "mouse_drag_selection.leftward_drag.svg");
    }

    [Fact]
    public void when_mouse_drag_leaves_editor_bounds_then_selection_continues_until_mouse_up()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        var editor = new TextEditor();
        root.Add(editor);
        root.GetRenderer().Arrange(new(0, 0, 20, 2));

        var screen = new AttentionManager(terminal, root, editor);
        screen.HandleText("hello world");

        screen.HandleMouseDown(1, 0, MouseButton.Left);
        screen.HandleMouseMove(30, 0);
        screen.HandleMouseUp(30, 0, MouseButton.Left);

        editor.SelectionRange.ShouldBe((1, 11));
        editor.CaretIndex.ShouldBe(11);

        var buffer = new ScreenBuffer(20, 2);
        var context = new RenderContext(new(new Screen()));
        editor.GetScribe().Draw(new Canvas(buffer, new(0, 0, 20, 2), context));
        buffer.WriteTerminalSnapshotSvg("mouse_drag_selection.outside_bounds_drag.svg");
        buffer.WriteLayoutDebugSvg(editor, 20, 2, "mouse_drag_selection.outside_bounds_drag.svg");
    }

    [Fact]
    public void when_mouse_drag_crosses_wrapped_line_then_selection_spans_multiple_visual_lines()
    {
        var terminal = new MockTerminal { WindowWidth = 5, WindowHeight = 2 };
        var root = new Screen();
        var editor = new TextEditor();
        root.Add(editor);

        var screen = new AttentionManager(terminal, root, editor);
        screen.HandleText("abcdefghij");

        screen.HandleMouseDown(1, 0, MouseButton.Left);
        screen.HandleMouseMove(2, 1);
        screen.HandleMouseUp(2, 1, MouseButton.Left);

        editor.SelectionRange.ShouldBe((1, 7));
        editor.CaretIndex.ShouldBe(7);

        var buffer = new ScreenBuffer(5, 2);
        var context = new RenderContext(new(new Screen()));
        editor.GetScribe().Draw(new Canvas(buffer, new(0, 0, 5, 2), context));
        buffer.WriteTerminalSnapshotSvg("mouse_drag_selection.multiline_wrapped_drag.svg");
        buffer.WriteLayoutDebugSvg(editor, 5, 2, "mouse_drag_selection.multiline_wrapped_drag.svg");
    }

    [Fact]
    public void when_mouse_drag_leaves_bounds_vertically_then_selection_clamps_to_end_until_mouse_up()
    {
        var terminal = new MockTerminal { WindowWidth = 5, WindowHeight = 2 };
        var root = new Screen();
        var editor = new TextEditor();
        root.Add(editor);

        var screen = new AttentionManager(terminal, root, editor);
        screen.HandleText("abcdefghij");

        screen.HandleMouseDown(1, 0, MouseButton.Left);
        screen.HandleMouseMove(2, 6);
        screen.HandleMouseUp(2, 6, MouseButton.Left);

        editor.SelectionRange.ShouldBe((1, 10));
        editor.CaretIndex.ShouldBe(10);

        var buffer = new ScreenBuffer(5, 2);
        var context = new RenderContext(new(new Screen()));
        editor.GetScribe().Draw(new Canvas(buffer, new(0, 0, 5, 2), context));
        buffer.WriteTerminalSnapshotSvg("mouse_drag_selection.multiline_outside_vertical_drag.svg");
        buffer.WriteLayoutDebugSvg(editor, 5, 2, "mouse_drag_selection.multiline_outside_vertical_drag.svg");
    }
}
