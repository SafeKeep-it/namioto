using Shouldly;
using Thoth.Rendering;
using Thoth.Widgets;

namespace comptatata.tests.app.cli.ui.thoth.components;

public class stack_panel_stacks_at_bottom_when_given_more_space : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void aligns_to_bottom_in_excess_space()
    {
        var panel = new StackPanel();
        var child1 = new FixedSizeWidget(10, 2);
        panel.Items.Add(child1);

        // Give it 10 lines of space, but it only needs 2.
        var engine = new FrameRenderer(fullRender: false);
        engine.RenderFrame(panel, new UiContext(panel), 10, 10, new Dictionary<IWidget, InvalidationKind>());

        engine.LayoutState.TryGetRect(child1, out var arrangedRect).ShouldBeTrue();
        arrangedRect.Y.ShouldBe(8);
    }

    class FixedSizeWidget(int width, int height) : TestWidgetBase
    {
        public override Size Measure(SizeConstraint constraint) => new(width, height);
        public override void Render(Canvas canvas) { }
    }
}
