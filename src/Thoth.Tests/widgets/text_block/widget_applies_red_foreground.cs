using Shouldly;
using Thoth.Widgets;

namespace comptatata.tests.app.cli.ui.thoth.components;

public class widget_applies_red_foreground : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void applies_foreground_color()
    {
        var block = new TextBlock
                    {
                        ForegroundColor = new Color(255, 0, 0),
                        BackgroundColor = new Color(0, 0, 255)
                    };
        var root = new Screen();
        root.Add(block);
        var ui = new UiContext(root);

        block.SetContent([new("Hi")]);

        var buffer = new ScreenBuffer(10, 2);
        var canvas = new Canvas(buffer, new(0, 0, 10, 2), new(ui));

        block.GetRenderer().Measure(new(10, 2));
        block.GetRenderer().Arrange(new(0, 0, 10, 2));
        block.GetScribe().Draw(canvas);

        // Foreground should be red
        var styleWithBoth = new Style(new Color(255, 0, 0), new Color(0, 0, 255));
        buffer.GetCell(0, 0).StyleIndex.ShouldBe(ui.Styles.Intern(styleWithBoth));
    }
}
