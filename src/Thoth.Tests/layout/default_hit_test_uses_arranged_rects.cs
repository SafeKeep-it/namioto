using Shouldly;
using Thoth.Rendering;
using Thoth.Rendering.Grid;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.layout;

public class default_hit_test_uses_arranged_rects : IAsyncLifetime
{
    public Task InitializeAsync()
    {
        var buffer = new ScreenBuffer(20, 10);
        var root = new hit_test_widget();
        var child = new hit_test_widget();

        root.Add(child);

        var (layout, renderBuffer) = Render(root);
        for (var y = 0; y < buffer.Height; y++)
            for (var x = 0; x < buffer.Width; x++)
                buffer.SetCell(x, y, renderBuffer.GetCell(x, y));

        buffer.WriteTerminalSnapshotSvg("default_hit_test_uses_arranged_rects_layout.svg");
        buffer.WriteLayoutDebugSvg(root, 20, 10, "default_hit_test_uses_arranged_rects_layout.svg");

        var childRect = LayoutRect(layout, child);
        var rootRect = LayoutRect(layout, root);

        childRect.ShouldNotBe(default);
        rootRect.ShouldNotBe(default);

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void finds_deepest_child_using_arranged_rect_offsets()
    {
        var root = new hit_test_widget();
        var child = new hit_test_widget();
        root.Add(child);

        var (layout, _) = Render(root);

        layout.WidgetAt(4, 3).ShouldBe(child);
    }

    [Fact]
    public void returns_parent_when_point_is_outside_child_but_inside_parent()
    {
        var root = new hit_test_widget();
        var child = new hit_test_widget();
        root.Add(child);

        var (layout, _) = Render(root);

        layout.WidgetAt(1, 1).ShouldBe(root);
    }

    [Fact]
    public void returns_null_when_point_is_outside_parent()
    {
        var root = new hit_test_widget();

        var (layout, _) = Render(root);

        layout.WidgetAt(40, 1).ShouldBe(SentinelWidget.Instance);
    }

    static (FrameLayoutState layoutState, GridBuffer buffer) Render(IWidget root)
    {
        var engine = new FrameRenderer(fullRender: false);
        var (buffer, _, _) = engine.RenderFrame(root,
                          new UiContext(root),
                          20,
                          10,
                          new Dictionary<IWidget, InvalidationKind>());
        return (engine.LayoutState, buffer);
    }

    static Rect LayoutRect(FrameLayoutState layoutState, IWidget widget)
    {
        layoutState.TryGetRect(widget, out var rect).ShouldBe(true);
        return rect;
    }

    sealed class hit_test_widget : IWidget
    {
        readonly hit_test_widget_renderer _renderer;
        readonly List<IWidget> _children = [];

        public hit_test_widget()
        {
            _renderer = new hit_test_widget_renderer(this);
        }

        public IWidget Parent { get; set; } = SentinelWidget.Instance;

        public IWidgetRenderer GetRenderer() => _renderer;

        public IWidgetScribe GetScribe() => _renderer;

        public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
        {
            for (var i = 0; i < _children.Count; i++)
            {
                if (!visitor.Visit(_children[i])) return;
            }
        }

        public void Add(IWidget child)
        {
            child.Parent = this;
            _children.Add(child);
        }

        public Size Measure(SizeConstraint constraint) =>
            new(constraint.MaxWidth, constraint.MaxHeight);

        public void Arrange(Rect rect)
        {
        }

        public void Render(Canvas canvas)
        {
        }

        sealed class hit_test_widget_renderer(hit_test_widget owner) : IWidgetRenderer, IWidgetScribe
        {
            readonly List<Canvas.ChildPlacement> _childPlacements = [];

            public Size Measure(SizeConstraint constraint) =>
                new(constraint.MaxWidth, constraint.MaxHeight);

            public void Arrange(Rect rect)
            {
                _childPlacements.Clear();
                for (var i = 0; i < owner._children.Count; i++)
                {
                    var childRect = new Rect(2, 2, Math.Max(1, rect.Width - 4), Math.Max(1, rect.Height - 4));
                    owner._children[i].GetRenderer().Arrange(childRect);
                    _childPlacements.Add(new(owner._children[i], childRect));
                }
            }

            public void Draw(Canvas canvas)
            {
                for (var i = 0; i < _childPlacements.Count; i++)
                {
                    var placement = _childPlacements[i];
                    canvas.RenderChild(owner, in placement);
                }
            }
        }
    }
}
