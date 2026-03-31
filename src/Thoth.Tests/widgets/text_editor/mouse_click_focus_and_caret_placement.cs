using Shouldly;
using Thoth;
using Thoth.Eventing;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class mouse_click_focus_and_caret_placement
{
    [Fact]
    public void when_click_targets_text_editor_then_keyboard_focus_routes_typing_to_editor()
    {
        var terminal = new MockTerminal();
        var root = new split_root_widget();
        var editor = new TextEditor();
        var plain = new plain_widget();
        root.SetChildren(editor, plain);

        var screen = new AttentionManager(terminal, root, keyboardFocus: null);
        screen.Render();
        screen.HandleMouseDown(1, 0, MouseButton.Left);
        screen.HandleText("ab");

        editor.Text.ShouldBe("ab");

        var buffer = new ScreenBuffer(10, 1);
        var context = new RenderContext(new(new Screen()));
        editor.GetScribe().Draw(new Canvas(buffer, new(0, 0, 10, 1), context));
        buffer.WriteTerminalSnapshotSvg("mouse_click_focus_and_caret_placement.focused_click.svg", cursorX: 2, cursorY: 0);
        buffer.WriteLayoutDebugSvg(editor,
                                   10,
                                   1,
                                   "mouse_click_focus_and_caret_placement.focused_click.svg",
                                   cursorX: 2,
                                   cursorY: 0);
    }

    [Fact]
    public void when_click_inside_text_editor_then_caret_moves_to_clicked_position()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        var editor = new TextEditor();
        root.Add(editor);

        var screen = new AttentionManager(terminal, root, editor);
        screen.Render();
        screen.HandleText("hello world");
        screen.HandleMouseDown(2, 0, MouseButton.Left);
        screen.HandleText("Z");

        editor.Text.ShouldBe("heZllo world");
        editor.CaretIndex.ShouldBe(3);
    }

    [Fact]
    public void when_mouse_down_inside_text_editor_then_caret_moves_to_hit_point_and_svg_is_written()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        var editor = new TextEditor();
        root.Add(editor);

        var screen = new AttentionManager(terminal, root, editor);
        screen.Render();
        screen.HandleText("hello world");
        screen.HandleMouseDown(2, 0, MouseButton.Left);

        editor.CaretIndex.ShouldBe(2);

        var buffer = new ScreenBuffer(12, 2);
        var context = new RenderContext(new(new Screen()));
        editor.GetScribe().Draw(new Canvas(buffer, new(0, 0, 12, 2), context));
        buffer.WriteTerminalSnapshotSvg("mouse_click_focus_and_caret_placement.mouse_down_moves_caret.svg",
                                        cursorX: 2,
                                        cursorY: 0);
        buffer.WriteLayoutDebugSvg(editor,
                                   12,
                                   2,
                                   "mouse_click_focus_and_caret_placement.mouse_down_moves_caret.svg",
                                   cursorX: 2,
                                   cursorY: 0);
    }

    [Fact]
    public void when_click_hits_wrapped_visual_line_then_caret_moves_to_visual_position()
    {
        var terminal = new MockTerminal { WindowWidth = 5, WindowHeight = 2 };
        var root = new Screen();
        var editor = new TextEditor();
        root.Add(editor);

        var screen = new AttentionManager(terminal, root, editor);
        screen.Render();
        screen.HandleText("abcdefghij");
        screen.HandleMouseDown(2, 1, MouseButton.Left);
        screen.HandleText("Z");

        editor.Text.ShouldBe("abcdefgZhij");
        editor.CaretIndex.ShouldBe(8);
    }

    sealed class split_root_widget : TestWidgetBase
    {
        TextEditor _left = null!;
        plain_widget _right = null!;

        public void SetChildren(TextEditor left, plain_widget right)
        {
            _left = left;
            _right = right;
            Add(left);
            Add(right);
        }

        public override void Arrange(Rect rect)
        {
            base.Arrange(rect);
            _left.GetRenderer().Arrange(new(0, 0, 5, 1));
            _right.GetRenderer().Arrange(new(5, 0, 5, 1));
        }

        public override void Render(Canvas canvas)
        {
        }
    }

    sealed class plain_widget : TestWidgetBase
    {
        public override void Render(Canvas canvas)
        {
        }
    }
}
