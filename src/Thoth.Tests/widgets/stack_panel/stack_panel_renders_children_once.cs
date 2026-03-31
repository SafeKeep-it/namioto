using Shouldly;
using Thoth.Tests.utilities;
using Thoth.Terminal.Raw.Egress;
using Thoth.Widgets;

namespace comptatata.tests.app.cli.ui.thoth.components;

public class stack_panel_renders_children_once
{
    [Fact]
    public void stack_panel_dispatches_child_rendering_without_fallback_duplication()
    {
        var panel = new StackPanel();
        var child = new render_count_widget();
        panel.Items.Add(child);

        var buffer = new ScreenBuffer(8, 2);
        tree_render_harness.Render(panel, buffer);

        child.RenderCount.ShouldBe(1);
    }

    sealed class render_count_widget : TestWidgetBase
    {
        public int RenderCount { get; private set; }

        public override Size Measure(SizeConstraint constraint) => new(constraint.MaxWidth, 1);

        public override void Render(Canvas canvas)
        {
            _ = canvas;
            RenderCount++;
        }
    }
}
