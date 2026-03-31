using Shouldly;
using Thoth.Widgets;

namespace comptatata.tests.app.cli.ui.thoth.components;

public class widget_applies_blue_background : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void applies_background_color()
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

        // Background should be blue
        var styleWithBackground = new Style(Background: new Color(0, 0, 255));
        buffer.GetCell(5, 0).StyleIndex.ShouldBe(ui.Styles.Intern(styleWithBackground));
    }
}
