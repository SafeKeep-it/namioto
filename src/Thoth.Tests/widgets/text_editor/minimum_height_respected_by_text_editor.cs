using Shouldly;
using Thoth;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class minimum_height_respected_by_text_editor
{
    readonly TextEditor _editor;
    readonly Size _initialMeasure;
    readonly Size _afterInputMeasure;

    public minimum_height_respected_by_text_editor()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        _editor = new TextEditor { MinHeight = 4 };
        root.Add(_editor);

        _initialMeasure = _editor.GetRenderer().Measure(new(16, 12));

        var screen = new AttentionManager(terminal, root, _editor);
        screen.HandleText("a");
        screen.HandleKey(new('\r', ConsoleKey.Enter, true, false, false));
        screen.HandleText("b");
        screen.HandleKey(new('\r', ConsoleKey.Enter, true, false, false));
        screen.HandleText("c");
        screen.HandleKey(new('\r', ConsoleKey.Enter, true, false, false));
        screen.HandleText("d");

        _afterInputMeasure = _editor.GetRenderer().Measure(new(16, 12));

        var buffer = new ScreenBuffer(16, 4);
        var context = new RenderContext(new(new Screen()));
        _editor.GetScribe().Draw(new Canvas(buffer, new(0, 0, 16, 4), context));
        buffer.WriteTerminalSnapshotSvg("minimum_height_respected_by_text_editor.min_height_4.svg");
        buffer.WriteLayoutDebugSvg(_editor, 16, 4, "minimum_height_respected_by_text_editor.min_height_4.svg");
    }

    [Fact]
    public void when_min_height_is_set_to_four_then_empty_editor_measures_to_four_rows()
    {
        _initialMeasure.Height.ShouldBe(4);
    }

    [Fact]
    public void when_four_lines_are_entered_then_editor_measures_to_four_rows()
    {
        _afterInputMeasure.Height.ShouldBe(4);
    }
}
