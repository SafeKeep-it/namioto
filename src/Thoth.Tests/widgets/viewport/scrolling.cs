using Shouldly;
using Thoth.Eventing;
using Thoth.Rendering;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.components;

public class scrolling : IAsyncLifetime
{
    TextEditor _content = null!;
    UiContext _uiContext = null!;
    Viewport _viewport = null!;

    public Task InitializeAsync()
    {
        _viewport = new();
        _viewport.GetRenderer().Arrange(new(0, 0, 10, 10));

        _content = new()
                   {
                       Text =
                           "Line 1\nLine 2\nLine 3\nLine 4\nLine 5\nLine 6\nLine 7\nLine 8\nLine 9\nLine 10\nLine 11\nLine 12"
                   };
        _viewport.Content = _content;
        _viewport.GetRenderer().Measure(new(10, 10));
        _viewport.GetRenderer().Arrange(new(0, 0, 10, 10));

        _uiContext = new(_viewport);
        _uiContext.KeyboardFocus = _content;

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void updates_offset()
    {
        var command = new ScrollIntoViewCommand(new(0, 11, 1, 1), _content);
        var dispatcher = new EventDispatcher();
        dispatcher.Dispatch(command.Sender, command);
        dispatcher.DispatchAll();

        _viewport.OffsetY.ShouldBe(2);

        var buffer = new ScreenBuffer(10, 10);
        var context = new RenderContext(new(new Screen()));
        _viewport.GetScribe().Draw(new Canvas(buffer, new(0, 0, 10, 10), context));
        buffer.WriteTerminalSnapshotSvg("scrolling.after_scroll_into_view.svg");
        buffer.WriteLayoutDebugSvg(_viewport, 10, 10, "scrolling.after_scroll_into_view.svg");
    }
}
