using System.Text;
using Shouldly;
using Thoth;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class control_c_clears_editor_input
{
    readonly TextEditor _editor;
    readonly string[] _lines;
    readonly Size _measuredSize;
    readonly ScreenBuffer _buffer;

    public control_c_clears_editor_input()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        _editor = new TextEditor();
        _editor.MinHeight = 1;
        root.Add(_editor);

        var screen = new AttentionManager(terminal, root, _editor);
        screen.HandleText("hello");
        screen.HandleKey(new('\r', ConsoleKey.Enter, true, false, false));
        screen.HandleText("world");
        screen.HandleKey(new('\0', ConsoleKey.C, false, false, true));

        _measuredSize = _editor.GetRenderer().Measure(new(16, 8));
        _buffer = new ScreenBuffer(16, 2);
        var context = new RenderContext(new(new Screen()));
        _editor.GetScribe().Draw(new Canvas(_buffer, new(0, 0, 16, 2), context));
        _lines = RenderLines(_buffer, 16, 2);

        _buffer.WriteTerminalSnapshotSvg("control_c_clears_editor_input.after_clear.svg");
        _buffer.WriteLayoutDebugSvg(_editor, 16, 2, "control_c_clears_editor_input.after_clear.svg");
    }

    [Fact]
    public void when_control_c_is_pressed_then_editor_text_is_cleared()
    {
        _editor.Text.ShouldBe(string.Empty);
    }

    [Fact]
    public void when_control_c_is_pressed_then_caret_returns_to_start()
    {
        _editor.CaretIndex.ShouldBe(0);
    }

    [Fact]
    public void when_control_c_is_pressed_then_editor_measurement_returns_to_single_row()
    {
        _measuredSize.Height.ShouldBe(1);
    }

    [Fact]
    public void when_control_c_is_pressed_then_rendered_rows_are_empty()
    {
        _lines[0].ShouldBe("                ");
        _lines[1].ShouldBe("                ");
    }

    static string[] RenderLines(ScreenBuffer buffer, int width, int height)
    {
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

        return lines;
    }
}
