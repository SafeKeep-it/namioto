using Shouldly;
using Thoth;
using Thoth.Terminal.Raw.Ingress;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class coalescing_routes_to_editor_and_paste_appends
{
    readonly TextEditor _editor;

    public coalescing_routes_to_editor_and_paste_appends()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        _editor = new TextEditor();
        root.Add(_editor);

        var screen = new AttentionManager(terminal, root, _editor);
        var apply = new ScreenOpApply(screen);

        apply.Apply(new(ScreenOpTarget.Editor,
                       ScreenOpKind.Key,
                       ScreenOpCoalesce.AppendText,
                       0,
                       0,
                       "hello"),
                  80,
                  25,
                  CancellationToken.None);

        apply.Apply(new(ScreenOpTarget.Editor,
                       ScreenOpKind.Paste,
                       ScreenOpCoalesce.None,
                       0,
                       0,
                       " world"),
                  80,
                  25,
                  CancellationToken.None);

        var buffer = new ScreenBuffer(16, 1);
        var context = new RenderContext(new(new Screen()));
        _editor.GetScribe().Draw(new Canvas(buffer, new(0, 0, 16, 1), context));
        buffer.WriteTerminalSnapshotSvg("coalescing_routes_to_editor_and_paste_appends.result.svg");
        buffer.WriteLayoutDebugSvg(_editor, 16, 1, "coalescing_routes_to_editor_and_paste_appends.result.svg");
    }

    [Fact]
    public void when_coalesced_text_and_paste_are_applied_then_editor_contains_combined_content()
    {
        _editor.Text.ShouldBe("hello world");
    }

    [Fact]
    public void when_coalesced_text_and_paste_are_applied_then_caret_moves_to_end_of_combined_content()
    {
        _editor.CaretIndex.ShouldBe(11);
    }
}
