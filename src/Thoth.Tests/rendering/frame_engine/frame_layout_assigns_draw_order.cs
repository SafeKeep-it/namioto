using Shouldly;
using Thoth.Rendering;
using Thoth.Widgets;

namespace comptatata.tests.app.cli.thoth.rendering.frame_engine;

public class frame_layout_assigns_draw_order : IAsyncLifetime
{
    readonly FrameRenderer _frameEngine;
    readonly Screen _root = new();
    readonly TextBar _first = new();
    readonly TextBar _second = new();
    FrameLayoutState _layoutState = null!;

    public frame_layout_assigns_draw_order()
    {
        _frameEngine = new(fullRender: false);
        _root.Add(_first);
        _root.Add(_second);
    }

    public Task InitializeAsync()
    {
        _frameEngine.RenderFrame(_root,
                                 new UiContext(_root),
                                 30,
                                 4,
                                 new Dictionary<IWidget, InvalidationKind>());
        _layoutState = _frameEngine.LayoutState;
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void arranged_rects_are_written_for_children()
    {
        _layoutState.TryGetRect(_first, out var firstRect).ShouldBeTrue();
        _layoutState.TryGetRect(_second, out var secondRect).ShouldBeTrue();
        firstRect.Width.ShouldBe(30);
        secondRect.Height.ShouldBe(4);
    }

    [Fact]
    public void draw_order_is_written_and_increases_by_traversal()
    {
        var rootOrder = _layoutState.DrawOrderOf(_root);
        var firstOrder = _layoutState.DrawOrderOf(_first);
        var secondOrder = _layoutState.DrawOrderOf(_second);

        rootOrder.ShouldBeLessThan(firstOrder);
        firstOrder.ShouldBeLessThan(secondOrder);
    }
}
