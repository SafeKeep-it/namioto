using Shouldly;
using Thoth.Widgets;

namespace Comptatata.tests.app.cli.ui.thoth.components;

public class wraps_content_at_max_width : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void wraps_text_at_specified_width()
    {
        var block = new TextBlock();
        var ui = new UiContext(block);
        var styleId = new StyleId(ui.Styles.Intern(new()));

        block.SetContent([new("Hello World", styleId)]);

        // Wrap at "Hello "
        var size = block.GetRenderer().Measure(new(6, 100));

        size.Width.ShouldBe(6); // "Hello "
        size.Height.ShouldBe(2); // "Hello ", "World"
    }
}