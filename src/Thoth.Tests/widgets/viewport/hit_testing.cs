using Shouldly;
using Thoth.Rendering;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.components;

public class hit_testing : IAsyncLifetime
{
    FrameLayoutState _layout = null!;
    MockWidget _content = null!;
    Viewport _viewport = null!;

    public Task InitializeAsync()
    {
        _viewport = new();
        _viewport.OffsetY = 100;

        _content = new();
        _viewport.Content = _content;

        var engine = new FrameRenderer(fullRender: false);
        engine.RenderFrame(_viewport,
                           new UiContext(_viewport),
                           20,
                           20,
                           new Dictionary<IWidget, InvalidationKind>());
        _layout = engine.LayoutState;

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void works_when_scrolled()
    {
        var target = _layout.WidgetAt(12, 12);
        target.ShouldBe(_content);
    }

    class MockWidget : TestWidgetBase
    {
        Rect? _rect;
        public int LastHitTestX { get; private set; }
        public int LastHitTestY { get; private set; }
        public override void Render(Canvas canvas) { }

        public override void Arrange(Rect rect)
        {
            _rect = rect;
            base.Arrange(rect);
        }

        public override IWidget? HitTest(int x, int y)
        {
            var rect = _rect ?? new Rect(0, 0, 100, 100);
            if (x < rect.X || x >= rect.X + rect.Width || y < rect.Y || y >= rect.Y + rect.Height)
                return null;
            LastHitTestX = x - rect.X;
            LastHitTestY = y - rect.Y;
            return this;
        }
    }
}
