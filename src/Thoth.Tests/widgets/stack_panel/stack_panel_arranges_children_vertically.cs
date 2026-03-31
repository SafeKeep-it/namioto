using Shouldly;
using Thoth.Rendering;
using Thoth.Widgets;

namespace comptatata.tests.app.cli.ui.thoth.components;

public class stack_panel_arranges_children_vertically : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void positions_children_top_to_bottom()
    {
        var panel = new StackPanel();
        var child1 = new FixedSizeWidget(10, 2);
        var child2 = new FixedSizeWidget(10, 3);
        panel.Items.Add(child1);
        panel.Items.Add(child2);

        var engine = new FrameRenderer(fullRender: false);
        engine.RenderFrame(panel, new UiContext(panel), 10, 5, new Dictionary<IWidget, InvalidationKind>());

        engine.LayoutState.TryGetRect(child1, out var child1Rect).ShouldBeTrue();
        engine.LayoutState.TryGetRect(child2, out var child2Rect).ShouldBeTrue();
        child1Rect.Y.ShouldBe(0);
        child2Rect.Y.ShouldBe(2);
    }

    class FixedSizeWidget(int width, int height) : TestWidgetBase
    {
        public override Size Measure(SizeConstraint constraint) => new(width, height);
        public override void Render(Canvas canvas) { }
    }
}
