using Shouldly;
using Thoth;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class word_selection_visualization
{
    readonly TextEditor _editor;
    readonly ScreenBuffer _buffer;

    public word_selection_visualization()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        _editor = new TextEditor();
        root.Add(_editor);

        var screen = new AttentionManager(terminal, root, _editor);
        screen.HandleText("hello world");
        screen.HandleKey(new('\0', ConsoleKey.Home, false, false, false));
        screen.HandleKey(new('\0', ConsoleKey.RightArrow, true, true, false));

        _buffer = new ScreenBuffer(16, 1);
        var context = new RenderContext(new(new Screen()));
        _editor.GetScribe().Draw(new Canvas(_buffer, new(0, 0, 16, 1), context));

        _buffer.WriteTerminalSnapshotSvg("word_selection_visualization.shift_alt_right.svg");
        _buffer.WriteLayoutDebugSvg(_editor, 16, 1, "word_selection_visualization.shift_alt_right.svg");
    }

    [Fact]
    public void when_shift_alt_right_selects_word_then_selected_cells_render_with_selection_style()
    {
        _editor.SelectionRange.ShouldBe((0, 5));

        var selectedStyleIndex = _buffer.GetCell(0, 0).StyleIndex;
        var unselectedStyleIndex = _buffer.GetCell(6, 0).StyleIndex;

        selectedStyleIndex.ShouldNotBe(unselectedStyleIndex);
    }
}
