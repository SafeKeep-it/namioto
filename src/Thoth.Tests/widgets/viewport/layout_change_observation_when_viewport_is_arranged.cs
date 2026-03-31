using Shouldly;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.components;

public class layout_change_observation_when_viewport_is_arranged
{
    readonly bool _layoutChangeHandled;
    readonly int _contentChangedCount;

    public layout_change_observation_when_viewport_is_arranged()
    {
        var dispatcher = new EventDispatcher();
        var observer = new observer_widget();
        var source = new source_widget();
        var viewport = new Viewport();

        observer.Add(viewport);
        viewport.Content = source;
        viewport.GetRenderer().Arrange(new(0, 0, 10, 4));

        dispatcher.Dispatch(source, new OnLayoutChanged());
        _layoutChangeHandled = dispatcher.EventContext.IsHandled;

        dispatcher.ProcessQueue();
        _contentChangedCount = observer.ContentChangedCount;
    }

    [Fact]
    public void when_layout_changes_and_viewport_is_arranged_then_viewport_emits_content_change()
    {
        _contentChangedCount.ShouldBe(1);
    }

    [Fact]
    public void when_layout_changes_and_viewport_is_arranged_then_layout_change_event_remains_unhandled_for_parent_propagation()
    {
        _layoutChangeHandled.ShouldBeFalse();
    }

    sealed class observer_widget : TestWidgetBase,
                                  IEventHandler<OnContentChanged>
    {
        public int ContentChangedCount { get; private set; }

        public void Handle(IEventContext ctx, in OnContentChanged e)
        {
            ContentChangedCount++;
        }

        public override void Render(Canvas canvas)
        {
        }
    }

    sealed class source_widget : TestWidgetBase
    {
        public override void Render(Canvas canvas)
        {
        }
    }

}
