using Shouldly;
using Thoth.Widgets;

namespace comptatata.tests.app.cli.ui.thoth.components;

public class handles_wide_characters_correctly : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void measures_wide_characters_as_double_width()
    {
        var block = new TextBlock();
        var ui = new UiContext(block);
        var styleId = new StyleId(ui.Styles.Intern(new()));

        // Emoji is W=2
        block.SetContent([new("🌟🌟🌟", styleId)]);

        var size = block.GetRenderer().Measure(new(100, 100));

        size.Width.ShouldBe(6);
        size.Height.ShouldBe(1);
    }
}