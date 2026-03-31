using System.Text;
using Shouldly;
using Thoth;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class keyboard_input_focus_routing
{
    [Fact]
    public void given_text_editor_is_keyboard_focus_when_typing_two_letters_then_editor_renders_them()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        var editor = new TextEditor();
        root.Add(editor);

        var screen = new AttentionManager(terminal, root, editor);
        screen.HandleKey(new('A', ConsoleKey.A, false, false, false));
        screen.HandleKey(new('B', ConsoleKey.B, false, false, false));

        var (rendered, buffer) = render_editor_line(editor, width: 10);
        rendered.ShouldBe("AB        ");
        buffer.WriteTerminalSnapshotSvg("keyboard_input_focus_routing.focused.svg");
        buffer.WriteLayoutDebugSvg(editor, 10, 1, "keyboard_input_focus_routing.focused.svg");
    }

    [Fact]
    public void given_keyboard_focus_is_null_when_typing_two_letters_then_autofocus_editor_renders_them()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        var editor = new TextEditor();
        root.Add(editor);

        var screen = new AttentionManager(terminal, root, keyboardFocus: null);
        screen.HandleKey(new('A', ConsoleKey.A, false, false, false));
        screen.HandleKey(new('B', ConsoleKey.B, false, false, false));

        var (rendered, buffer) = render_editor_line(editor, width: 10);
        rendered.ShouldBe("AB        ");
        buffer.WriteTerminalSnapshotSvg("keyboard_input_focus_routing.autofocus.svg");
        buffer.WriteLayoutDebugSvg(editor, 10, 1, "keyboard_input_focus_routing.autofocus.svg");
    }

    [Fact]
    public void given_text_editor_is_keyboard_focus_when_shift_enter_then_next_letter_renders_on_next_line()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        var editor = new TextEditor();
        root.Add(editor);

        var screen = new AttentionManager(terminal, root, editor);
        screen.HandleKey(new('A', ConsoleKey.A, false, false, false));
        screen.HandleKey(new('\r', ConsoleKey.Enter, true, false, false));
        screen.HandleKey(new('B', ConsoleKey.B, false, false, false));

        var (lines, buffer) = render_editor_lines(editor, width: 10, height: 2);
        lines[0].ShouldBe("A         ");
        lines[1].ShouldBe("B         ");

        buffer.WriteTerminalSnapshotSvg("keyboard_input_focus_routing.shift_enter.svg");
        buffer.WriteLayoutDebugSvg(editor, 10, 2, "keyboard_input_focus_routing.shift_enter.svg");
    }

    static (string text, ScreenBuffer buffer) render_editor_line(TextEditor editor, int width)
    {
        var buffer = new ScreenBuffer(width, 1);
        var context = new RenderContext(new(new Screen()));
        var canvas = new Canvas(buffer, new(0, 0, width, 1), context);
        editor.GetScribe().Draw(canvas);

        var sb = new StringBuilder(width);
        for (var x = 0; x < width; x++)
        {
            var cell = buffer.GetCell(x, 0);
            sb.Append(cell.GlyphId == 0 ? ' ' : (char)cell.GlyphId);
        }

        return (sb.ToString(), buffer);
    }

    static (string[] lines, ScreenBuffer buffer) render_editor_lines(TextEditor editor, int width, int height)
    {
        var buffer = new ScreenBuffer(width, height);
        var context = new RenderContext(new(new Screen()));
        var canvas = new Canvas(buffer, new(0, 0, width, height), context);
        editor.GetScribe().Draw(canvas);

        var lines = new string[height];
        for (var y = 0; y < height; y++)
        {
            var sb = new StringBuilder(width);
            for (var x = 0; x < width; x++)
            {
                var cell = buffer.GetCell(x, y);
                sb.Append(cell.GlyphId == 0 ? ' ' : (char)cell.GlyphId);
            }

            lines[y] = sb.ToString();
        }

        return (lines, buffer);
    }
}
