using Shouldly;
using Thoth.Rendering;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.layout;

public class arranged_rects_storage : IAsyncLifetime
{
    public Task InitializeAsync()
    {
        var buffer = new ScreenBuffer(20, 3);
        var widget = new TextBar { LeftTitle = "arranged_rects_storage" };
        widget.GetRenderer().Arrange(new(0, 0, 20, 1));
        widget.GetScribe().Draw(new Canvas(buffer, new(0, 0, 20, 3), new(new(new Screen()))));
        buffer.WriteTerminalSnapshotSvg("arranged_rects_storage_layout.svg");
        buffer.WriteLayoutDebugSvg(widget, 20, 3, "arranged_rects_storage_layout.svg");
        return Task.CompletedTask;
    }
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void returns_null_before_arrange()
    {
        var engine = new FrameRenderer(fullRender: false);
        var widget = new TextBar();

        engine.LayoutState.TryGetRect(widget, out var rect).ShouldBe(false);
        rect.ShouldBe(default);
    }

    [Fact]
    public void stores_rect_when_widget_is_arranged()
    {
        var widget = new TextBar();
        var engine = new FrameRenderer(fullRender: false);

        engine.RenderFrame(widget, new UiContext(widget), 11, 1, new Dictionary<IWidget, InvalidationKind>());

        ArrangedRectsState(engine, widget).ShouldBe(new Rect(0, 0, 11, 1));
    }

    [Fact]
    public void updates_rect_when_arranged_again()
    {
        var widget = new TextBar();
        var engine = new FrameRenderer(fullRender: false);

        engine.RenderFrame(widget, new UiContext(widget), 5, 1, new Dictionary<IWidget, InvalidationKind>());

        ArrangedRectsState(engine, widget).ShouldBe(new Rect(0, 0, 5, 1));

        engine.RenderFrame(widget, new UiContext(widget), 9, 1, new Dictionary<IWidget, InvalidationKind>());

        ArrangedRectsState(engine, widget).ShouldBe(new Rect(0, 0, 9, 1));
    }

    static Rect ArrangedRectsState(FrameRenderer engine, IWidget widget)
    {
        engine.LayoutState.TryGetRect(widget, out var rect).ShouldBe(true);
        return rect;
    }
}
