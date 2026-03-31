using System.Text;
using Shouldly;
using Thoth;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class word_navigation_next_word_from_middle
{
    readonly TextEditor _editor;
    readonly string _rendered;

    public word_navigation_next_word_from_middle()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        _editor = new TextEditor();
        root.Add(_editor);

        var screen = new AttentionManager(terminal, root, _editor);
        screen.HandleText("hello world");
        screen.HandleKey(new('\0', ConsoleKey.Home, false, false, false));
        screen.HandleKey(new('\0', ConsoleKey.RightArrow, false, false, false));
        screen.HandleKey(new('\0', ConsoleKey.RightArrow, false, false, false));
        screen.HandleKey(new('\0', ConsoleKey.RightArrow, false, true, false));

        var buffer = new ScreenBuffer(16, 1);
        var context = new RenderContext(new(new Screen()));
        _editor.GetScribe().Draw(new Canvas(buffer, new(0, 0, 16, 1), context));
        _rendered = render_line(buffer, 16);

        buffer.WriteTerminalSnapshotSvg("word_navigation_next_word_from_middle.alt_right.svg");
        buffer.WriteLayoutDebugSvg(_editor, 16, 1, "word_navigation_next_word_from_middle.alt_right.svg");
    }

    [Fact]
    public void when_alt_right_is_pressed_in_middle_of_word_then_caret_moves_to_end_of_word()
    {
        _editor.CaretIndex.ShouldBe(5);
        _rendered.ShouldStartWith("hello world");
    }

    static string render_line(ScreenBuffer buffer, int width)
    {
        var sb = new StringBuilder(width);
        for (var x = 0; x < width; x++)
        {
            var cell = buffer.GetCell(x, 0);
            sb.Append(cell.GlyphId == 0 ? ' ' : (char)cell.GlyphId);
        }

        return sb.ToString();
    }
}
