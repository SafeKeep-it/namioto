using Shouldly;
using Thoth.Rendering;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.thoth.invalidation.rendering;

public class skip_clean_widget : IAsyncLifetime
{
    readonly UiContext _uiContext;
    readonly MockWidget _widget;

    public skip_clean_widget()
    {
        _widget = new();
        var screen = new Screen();
        screen.Add(_widget);
        _uiContext = new(screen);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void render_not_called_when_clean()
    {
        var buffer = new ScreenBuffer(10, 10);
        var invalidations = new Dictionary<IWidget, InvalidationKind>();
        var renderContext = new RenderContext(_uiContext, invalidations);
        var canvas = new Canvas(buffer, new(0, 0, 10, 10), renderContext);

        _widget.Reset();
        _widget.GetScribe().Draw(canvas);
        _widget.RenderCalled.ShouldBeFalse();
    }

    [Fact]
    public void render_called_when_invalidated()
    {
        var buffer = new ScreenBuffer(10, 10);
        var invalidations = new Dictionary<IWidget, InvalidationKind>
                            {
                                { _widget, InvalidationKind.Content }
                            };
        var renderContext = new RenderContext(_uiContext, invalidations);
        var canvas = new Canvas(buffer, new(0, 0, 10, 10), renderContext);

        _widget.Reset();
        _widget.GetScribe().Draw(canvas);
        _widget.RenderCalled.ShouldBeTrue();
    }

    class MockWidget : IWidget
    {
        readonly IWidgetScribe _scribe;
        readonly IWidgetRenderer _renderer;

        public MockWidget()
        {
            _renderer = new mock_widget_renderer(this);
            _scribe = new MockWidgetScribe(this);
        }

        public IWidget Parent { get; set; } = SentinelWidget.Instance;
        public bool RenderCalled { get; private set; }

        public IWidgetRenderer GetRenderer() => _renderer;

        public IWidgetScribe GetScribe() => _scribe;

        public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
        {
        }

        public Size Measure(SizeConstraint constraint) => new(constraint.MaxWidth, constraint.MaxHeight);

        public void Arrange(Rect rect)
        {
        }

        sealed class MockWidgetScribe(MockWidget owner) : IWidgetScribe
        {
            public void Draw(Canvas canvas)
            {
                if (canvas.Context.GetInvalidation(owner) != InvalidationKind.None)
                    owner.RenderCalled = true;
            }
        }

        sealed class mock_widget_renderer(MockWidget owner) : IWidgetRenderer
        {
            public Size Measure(SizeConstraint constraint) =>
                new(constraint.MaxWidth, constraint.MaxHeight);

            public void Arrange(Rect rect)
            {
                _ = rect;
            }

            public void Draw(Canvas canvas)
            {
                owner._scribe.Draw(canvas);
            }
        }

        public void Reset() => RenderCalled = false;
    }
}
