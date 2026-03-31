using Shouldly;
using Thoth;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Widgets;
using Thoth.Tests.utilities;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class initial_keyboard_focus_emits_focus_event
{
    [Fact]
    public void when_initial_keyboard_focus_is_set_then_target_receives_focus_event()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        var observer = new focus_observer_widget();
        root.Add(observer);

        _ = new AttentionManager(terminal, root, observer);

        observer.FocusCount.ShouldBe(1);
        observer.BlurCount.ShouldBe(0);
    }

    [Fact]
    public void when_initial_keyboard_focus_is_null_then_no_focus_event_is_emitted()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        var observer = new focus_observer_widget();
        root.Add(observer);

        _ = new AttentionManager(terminal, root, keyboardFocus: null);

        observer.FocusCount.ShouldBe(0);
        observer.BlurCount.ShouldBe(0);
    }

    sealed class focus_observer_widget : TestWidgetBase,
                                         IEventHandler<OnFocus>,
                                         IEventHandler<OnBlur>
    {
        public int FocusCount { get; private set; }
        public int BlurCount { get; private set; }

        public void Handle(IEventContext ctx, in OnFocus e)
        {
            FocusCount++;
        }

        public void Handle(IEventContext ctx, in OnBlur e)
        {
            BlurCount++;
        }

        public override void Render(Canvas canvas)
        {
        }
    }
}
