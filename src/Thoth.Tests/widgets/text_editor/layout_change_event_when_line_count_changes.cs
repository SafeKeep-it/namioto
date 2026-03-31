using Shouldly;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Widgets;
using Thoth.Tests.utilities;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class layout_change_event_when_line_count_changes
{
    readonly observer_widget _observer;

    public layout_change_event_when_line_count_changes()
    {
        _observer = new();
        var editor = new TextEditor();
        _observer.Add(editor);

        var dispatcher = new EventDispatcher();
        dispatcher.Dispatch(editor, new KeyPressedInput(new('A', ConsoleKey.A, false, false, false)));
        dispatcher.Dispatch(editor, new KeyPressedInput(new('\r', ConsoleKey.Enter, true, false, false)));
        dispatcher.Dispatch(editor, new KeyPressedInput(new('B', ConsoleKey.B, false, false, false)));
        dispatcher.ProcessQueue();

        var buffer = new ScreenBuffer(20, 3);
        var context = new RenderContext(new(new Screen()));
        editor.GetScribe().Draw(new Canvas(buffer, new(0, 0, 20, 3), context));
        buffer.WriteTerminalSnapshotSvg("layout_change_event_when_line_count_changes.shift_enter.svg");
        buffer.WriteLayoutDebugSvg(editor, 20, 3, "layout_change_event_when_line_count_changes.shift_enter.svg");
    }

    [Fact]
    public void when_line_count_changes_then_layout_change_event_is_emitted()
    {
        _observer.LayoutChangeCount.ShouldBe(1);
    }

    [Fact]
    public void when_line_count_changes_then_content_change_event_is_still_emitted()
    {
        _observer.ContentChangeCount.ShouldBe(3);
    }

    sealed class observer_widget : TestWidgetBase,
                                  IEventHandler<OnContentChanged>,
                                  IEventHandler<OnLayoutChanged>
    {
        public int ContentChangeCount { get; private set; }
        public int LayoutChangeCount { get; private set; }

        public void Handle(IEventContext ctx, in OnContentChanged e)
        {
            ContentChangeCount++;
        }

        public void Handle(IEventContext ctx, in OnLayoutChanged e)
        {
            LayoutChangeCount++;
        }

        public override void Render(Canvas canvas)
        {
        }
    }
}
