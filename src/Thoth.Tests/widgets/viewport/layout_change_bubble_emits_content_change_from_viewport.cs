using Shouldly;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Rendering;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.components;

public class layout_change_bubble_emits_content_change_from_viewport
{
    readonly EventDispatcher _dispatcher;
    readonly Screen _root;
    readonly Viewport _viewport;
    readonly source_widget _source;

    public layout_change_bubble_emits_content_change_from_viewport()
    {
        _dispatcher = new();
        _root = new();
        _viewport = new();
        _source = new();

        _viewport.Content = _source;
        _root.Add(_viewport);

        var engine = new FrameRenderer(fullRender: false);
        var uiContext = new UiContext(_root);
        engine.RenderFrame(_root,
                           uiContext,
                           20,
                           6,
                           new Dictionary<IWidget, InvalidationKind>());

        _dispatcher.SetLayoutState(engine.LayoutState);
        _dispatcher.Dispatch(_source, new OnLayoutChanged());
        _dispatcher.ProcessQueue();

        var (_, _, _) = engine.RenderFrame(_root,
                                           uiContext,
                                           20,
                                           6,
                                           _dispatcher.EventContext.Invalidations);

        var debugBuffer = new ScreenBuffer(20, 6);
        tree_render_harness.Render(_root, debugBuffer, uiContext);
        debugBuffer.WriteLayoutDebugSvg(_root,
                                        20,
                                        6,
                                        "layout_change_bubble_emits_content_change_from_viewport.svg",
                                        engine.LayoutState);
        terminal_snapshot_assertions.WriteInvalidationOverlaySvg(_root,
                                                                 20,
                                                                 6,
                                                                 "layout_change_bubble_emits_content_change_from_viewport.svg",
                                                                 engine.LayoutState,
                                                                 _dispatcher.EventContext.Invalidations);
    }

    [Fact]
    public void viewport_receives_content_invalidation_from_bubbled_layout_change()
    {
        _dispatcher.EventContext.Invalidations.TryGetValue(_viewport, out var kind).ShouldBeTrue();
        (kind & InvalidationKind.Content).ShouldBe(InvalidationKind.Content);
    }

    [Fact]
    public void source_does_not_keep_layout_invalidation_when_viewport_replaces_it_with_content_change()
    {
        _dispatcher.EventContext.Invalidations.ContainsKey(_source).ShouldBeFalse();
    }

    sealed class source_widget : TestWidgetBase
    {
        public override void Render(Canvas canvas)
        {
        }
    }
}
