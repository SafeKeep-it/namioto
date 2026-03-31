using Shouldly;
using Thoth;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class selected_text_replacement_on_typing
{
    readonly TextEditor _editor;

    public selected_text_replacement_on_typing()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        _editor = new TextEditor();
        root.Add(_editor);

        var screen = new AttentionManager(terminal, root, _editor);
        screen.HandleText("hello world");
        screen.HandleKey(new('\0', ConsoleKey.LeftArrow, true, true, false));
        screen.HandleKey(new('Z', ConsoleKey.Z, false, false, false));

        var buffer = new ScreenBuffer(16, 1);
        var context = new RenderContext(new(new Screen()));
        _editor.GetScribe().Draw(new Canvas(buffer, new(0, 0, 16, 1), context));
        buffer.WriteTerminalSnapshotSvg("selected_text_replacement_on_typing.replace.svg");
        buffer.WriteLayoutDebugSvg(_editor, 16, 1, "selected_text_replacement_on_typing.replace.svg");
    }

    [Fact]
    public void when_character_is_typed_with_word_selected_then_selected_text_is_replaced()
    {
        _editor.Text.ShouldBe("hello Z");
    }

    [Fact]
    public void when_character_replaces_selection_then_caret_moves_after_inserted_character()
    {
        _editor.CaretIndex.ShouldBe(7);
    }

    [Fact]
    public void when_character_replaces_selection_then_selection_is_cleared()
    {
        _editor.SelectionRange.ShouldBeNull();
    }
}
