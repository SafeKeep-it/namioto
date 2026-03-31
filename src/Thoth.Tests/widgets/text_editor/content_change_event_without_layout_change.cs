using Shouldly;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Widgets;
using Thoth.Tests.utilities;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class content_change_event_without_layout_change
{
    readonly observer_widget _observer;
    readonly Size _before;
    readonly Size _after;

    public content_change_event_without_layout_change()
    {
        _observer = new();
        var editor = new TextEditor();
        _observer.Add(editor);

        _before = editor.GetRenderer().Measure(new(20, 10));

        var dispatcher = new EventDispatcher();
        dispatcher.Dispatch(editor, new KeyPressedInput(new('A', ConsoleKey.A, false, false, false)));
        dispatcher.ProcessQueue();

        _after = editor.GetRenderer().Measure(new(20, 10));

        var buffer = new ScreenBuffer(20, 3);
        var context = new RenderContext(new(new Screen()));
        editor.GetScribe().Draw(new Canvas(buffer, new(0, 0, 20, 3), context));
        buffer.WriteTerminalSnapshotSvg("content_change_event_without_layout_change.input.svg");
        buffer.WriteLayoutDebugSvg(editor, 20, 3, "content_change_event_without_layout_change.input.svg");
    }

    [Fact]
    public void when_text_changes_without_size_change_then_content_change_event_is_emitted()
    {
        _observer.ContentChangeCount.ShouldBe(1);
    }

    [Fact]
    public void when_text_changes_without_size_change_then_layout_change_event_is_not_emitted()
    {
        _observer.LayoutChangeCount.ShouldBe(0);
    }

    [Fact]
    public void when_text_changes_without_size_change_then_measured_size_remains_the_same()
    {
        _after.ShouldBe(_before);
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
