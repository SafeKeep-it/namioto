using Shouldly;
using Thoth.Rendering;
using Thoth.Widgets;

namespace comptatata.tests.app.cli.ui.thoth.components;

public class stack_panel_positions_content_right_aligned : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void positions_textblock_on_right_edge()
    {
        // Arrange
        var stackPanel = new StackPanel();
        var textBlock = new TextBlock
                        {
                            Text = "Hi",
                            WidthSizeMode = WidthSizeMode.Content,
                            HorizontalAlignment = HorizontalAlignment.Right
                        };
        var align = new Thoth.Widgets.Layout.Align
                    {
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Content = textBlock
                    };
        stackPanel.Items.Add(align);

        // Act
        // Height 10, Width 10
        var engine = new FrameRenderer(fullRender: false);
        engine.RenderFrame(stackPanel,
                           new UiContext(stackPanel),
                           10,
                           10,
                           new Dictionary<IWidget, InvalidationKind>());

        // Assert
        // "Hi" is 2 chars.
        var size = textBlock.GetRenderer().Measure(new(10, 10));
        size.Width.ShouldBe(2);

        engine.LayoutState.TryGetRect(textBlock, out var arrangedRect).ShouldBeTrue();
        arrangedRect.X.ShouldBe(8); // 10 - 2
        arrangedRect.Width.ShouldBe(2);
    }
}
