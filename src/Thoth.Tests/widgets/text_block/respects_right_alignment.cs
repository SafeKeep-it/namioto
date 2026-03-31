using Shouldly;
using Thoth.Widgets;

namespace comptatata.tests.app.cli.ui.thoth.components;

public class respects_right_alignment : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void right_aligns_content()
    {
        var block = new TextBlock();
        var root = new Screen();
        root.Add(block);
        var ui = new UiContext(root);
        var styleId = new StyleId(ui.Styles.Intern(new()));

        block.Align = Align.Right;
        block.SetContent([new("Hi", styleId)]);

        var buffer = new ScreenBuffer(10, 2);
        var canvas = new Canvas(buffer, new(0, 0, 10, 2), new(ui));

        block.GetRenderer().Measure(new(10, 2));
        block.GetRenderer().Arrange(new(0, 0, 10, 2));
        block.GetScribe().Draw(canvas);

        // "Hi" has width 2. Canvas width is 10.
        // Right alignment means x = 10 - 2 = 8.
        buffer.GetCell(8, 0).GlyphId.ShouldBe('H');
        buffer.GetCell(9, 0).GlyphId.ShouldBe('i');
    }
}
