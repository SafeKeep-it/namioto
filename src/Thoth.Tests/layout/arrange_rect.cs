using Shouldly;
using Thoth.Rendering;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.arrange_phase;

public class arrange_rect : IAsyncLifetime
{
    Dock _bottomDock = null!;
    ScreenBuffer _buffer = null!;
    RenderContext _context = null!;
    Dock _fillDock = null!;
    FrameLayoutState _layout = null!;

    Dock _topDock = null!;

    public Task InitializeAsync()
    {
        _buffer = new(10, 5);
        _context = new(new(new Screen()));
        var canvas = new Canvas(_buffer, new(0, 0, 10, 5), _context);

        var dockPanel = new DockPanel();

        var top = new Dock { Position = DockPosition.Top, Content = new MockWidget(new(10, 1)) };
        var bottom = new Dock
                     {
                         Position = DockPosition.Bottom, Content = new MockWidget(new(10, 1))
                     };
        var fill = new Dock { Position = DockPosition.Fill, Content = new MockWidget(new(10, 3)) };

        dockPanel.Add(top);
        dockPanel.Add(bottom);
        dockPanel.Add(fill);

        _layout = tree_render_harness.Render(dockPanel, _buffer, _context.UiContext);
        dockPanel.GetScribe().Draw(canvas);
        _buffer.WriteTerminalSnapshotSvg("arrange_rect_layout.svg");
        _buffer.WriteLayoutDebugSvg(dockPanel, 10, 5, "arrange_rect_layout.svg");

        _topDock = top;
        _bottomDock = bottom;
        _fillDock = fill;

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void top_has_correct_rect()
    {
        _layout.TryGetRect(_topDock, out var topRect).ShouldBeTrue();
        _layout.TryGetRect(_topDock.Content!, out var topContentRect).ShouldBeTrue();
        topRect.ShouldBe(new Rect(0, 0, 10, 1));
        topContentRect.ShouldBe(new Rect(0, 0, 10, 1));
    }

    [Fact]
    public void bottom_has_correct_rect()
    {
        _layout.TryGetRect(_bottomDock, out var bottomRect).ShouldBeTrue();
        _layout.TryGetRect(_bottomDock.Content!, out var bottomContentRect).ShouldBeTrue();
        bottomRect.ShouldBe(new Rect(0, 4, 10, 1));
        bottomContentRect.ShouldBe(new Rect(0, 4, 10, 1));
    }
}

public class MockWidget(Size desiredSize) : TestWidgetBase
{
    public override Size Measure(SizeConstraint constraint) => desiredSize;
    public override void Render(Canvas canvas) { }
}
