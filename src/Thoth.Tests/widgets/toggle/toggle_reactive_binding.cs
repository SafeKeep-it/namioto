using Shouldly;
using Thoth.Bindings;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Thoth.Tests.widgets.toggle;

public class toggle_reactive_binding
{
    [Fact]
    public void external_observable_change_updates_toggle_state()
    {
        var state = new Observable<bool>(false);
        var toggle = new Toggle { IsChecked = state };

        state.Set(true);

        ((bool)toggle.IsChecked).ShouldBeTrue();
        ReferenceEquals(toggle.IsChecked, state).ShouldBeTrue();
    }

    [Fact]
    public void mouse_click_updates_bound_observable_state()
    {
        var state = new Observable<bool>(false);
        var toggle = new Toggle { IsChecked = state };
        var dispatcher = new EventDispatcher();

        dispatcher.Dispatch(toggle, new OnMouseClick());
        dispatcher.ProcessQueue();

        ((bool)state).ShouldBeTrue();
        ((bool)toggle.IsChecked).ShouldBeTrue();
    }

    [Fact]
    public void mouse_click_raises_content_change_on_bound_toggle()
    {
        var state = new Observable<bool>(false);
        var observer = new observer_widget();
        var toggle = new Toggle { IsChecked = state };
        observer.Add(toggle);

        var dispatcher = new EventDispatcher();
        dispatcher.Dispatch(toggle, new OnMouseClick());
        dispatcher.ProcessQueue();

        observer.ContentChangeCount.ShouldBe(1);
        dispatcher.EventContext.Invalidations.TryGetValue(toggle, out var kind).ShouldBeTrue();
        (kind & InvalidationKind.Content).ShouldBe(InvalidationKind.Content);
    }

    [Fact]
    public void flushing_pending_value_binding_updates_raises_content_change()
    {
        BindingUpdateQueue.Clear();

        var state = new Observable<bool>(false);
        var observer = new observer_widget();
        var toggle = new Toggle { IsChecked = state };
        observer.Add(toggle);
        var dispatcher = new EventDispatcher();

        state.Set(true);
        BindingUpdateQueue.Flush(dispatcher).ShouldBeTrue();
        dispatcher.ProcessQueue();

        observer.ContentChangeCount.ShouldBe(1);
        dispatcher.EventContext.Invalidations.TryGetValue(toggle, out var kind).ShouldBeTrue();
        (kind & InvalidationKind.Content).ShouldBe(InvalidationKind.Content);
    }

    [Fact]
    public void pruning_detached_binding_widgets_removes_pending_updates()
    {
        BindingUpdateQueue.Clear();

        var state = new Observable<bool>(false);
        var observer = new observer_widget();
        var toggle = new Toggle { IsChecked = state };
        observer.Add(toggle);
        observer.Remove(toggle);
        var dispatcher = new EventDispatcher();

        state.Set(true);
        BindingUpdateQueue.PruneDetached(observer);

        BindingUpdateQueue.Flush(dispatcher).ShouldBeFalse();
        dispatcher.ProcessQueue();

        observer.ContentChangeCount.ShouldBe(0);
        dispatcher.EventContext.Invalidations.ContainsKey(toggle).ShouldBeFalse();
    }

    sealed class observer_widget : TestWidgetBase,
                                  IEventHandler<OnContentChanged>,
                                  IEventHandler<OnLayoutChanged>
    {
        public int ContentChangeCount { get; private set; }

        public int LayoutChangeCount { get; private set; }

        public void Handle(IEventContext context, in OnContentChanged e)
        {
            ContentChangeCount++;
        }

        public void Handle(IEventContext context, in OnLayoutChanged e)
        {
            LayoutChangeCount++;
        }

        public override void Render(Canvas canvas)
        {
        }
    }

}
