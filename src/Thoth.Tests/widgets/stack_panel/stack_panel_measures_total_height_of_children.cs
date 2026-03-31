using Shouldly;
using Thoth.Widgets;

namespace comptatata.tests.app.cli.ui.thoth.components;

public class stack_panel_measures_total_height_of_children : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void measures_sum_of_child_heights()
    {
        var panel = new StackPanel();
        panel.Items.Add(new FixedSizeWidget(10, 2));
        panel.Items.Add(new FixedSizeWidget(10, 3));

        var size = panel.GetRenderer().Measure(new(10, 100));

        size.Height.ShouldBe(5);
        size.Width.ShouldBe(10);
    }

    class FixedSizeWidget(int width, int height) : TestWidgetBase
    {
        public override Size Measure(SizeConstraint constraint) => new(width, height);
        public override void Render(Canvas canvas) { }
    }
}
