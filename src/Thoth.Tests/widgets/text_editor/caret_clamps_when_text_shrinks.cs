using Shouldly;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class caret_clamps_when_text_shrinks
{
    readonly TextEditor _editor;

    public caret_clamps_when_text_shrinks()
    {
        _editor = new TextEditor();
        _editor.Text = "hello world";
        _editor.CaretIndex = 11;
        _editor.Text = "a";
    }

    [Fact]
    public void when_text_becomes_shorter_than_caret_position_then_caret_is_clamped_to_text_length()
    {
        _editor.CaretIndex.ShouldBe(1);
    }

    [Fact]
    public void when_text_becomes_shorter_then_selection_is_cleared()
    {
        _editor.SelectionRange.ShouldBeNull();
    }
}
