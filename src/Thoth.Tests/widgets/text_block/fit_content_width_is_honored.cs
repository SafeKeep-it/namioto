using Shouldly;
using Thoth.Widgets;

namespace comptatata.tests.app.cli.ui.thoth.components;

public class fit_content_width_is_honored : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void measures_exact_content_width()
    {
        var block = new TextBlock();
        var ui = new UiContext(block);

        block.SetContent([new("Hello")]);

        var size = block.GetRenderer().Measure(new(100, 100));

        size.Width.ShouldBe(5);
        size.Height.ShouldBe(1);
    }
}