using Shouldly;
using Thoth.Rendering;
using Thoth.Widgets;

namespace comptatata.tests.app.cli.ui.thoth.components;

public class stack_panel_positions_border_right_aligned : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void positions_border_with_content_on_right_edge()
    {
        // Arrange
        var stackPanel = new StackPanel();
        var textBlock = new TextBlock { Text = "Hi" };
        var border = new Border
                     {
                         Content = textBlock,
                         WidthSizeMode = WidthSizeMode.Content,
                         HorizontalAlignment = HorizontalAlignment.Right
                     };
        var align = new Thoth.Widgets.Layout.Align
                    {
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Content = border
                    };
        stackPanel.Items.Add(align);

        // Act
        var engine = new FrameRenderer(fullRender: false);
        engine.RenderFrame(stackPanel,
                           new UiContext(stackPanel),
                           10,
                           10,
                           new Dictionary<IWidget, InvalidationKind>());

        // Assert
        // "Hi" is 2 chars. Border adds 2 -> 4.
        var size = border.GetRenderer().Measure(new(10, 10));
        size.Width.ShouldBe(4);

        engine.LayoutState.TryGetRect(border, out var arrangedRect).ShouldBeTrue();
        arrangedRect.X.ShouldBe(6); // 10 - 4
        arrangedRect.Width.ShouldBe(4);
    }
}
