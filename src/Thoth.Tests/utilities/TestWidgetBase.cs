using Thoth.Widgets;
using Thoth.Rendering;

namespace Thoth.Tests.utilities;

public abstract class TestWidgetBase : IWidget
{
    readonly IWidgetScribe _scribe;
    readonly IWidgetRenderer _renderer;
    readonly List<IWidget> _children = [];
    readonly List<Canvas.ChildPlacement> _childPlacements = [];

    protected TestWidgetBase()
    {
        _renderer = new test_widget_renderer(this);
        _scribe = new TestWidgetBaseScribe(this);
    }

    public IWidget Parent { get; set; } = SentinelWidget.Instance;

    public IWidgetRenderer GetRenderer() => _renderer;

    public IWidgetScribe GetScribe() => _scribe;

    public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
    {
        for (var i = 0; i < _children.Count; i++)
        {
            if (!visitor.Visit(_children[i])) return;
        }
    }

    public virtual void Add(IWidget child)
    {
        child.Parent = this;
        _children.Add(child);
    }

    public virtual void Remove(IWidget child)
    {
        child.Parent = SentinelWidget.Instance;
        _children.Remove(child);
    }

    public virtual Size Measure(SizeConstraint constraint) =>
        new(constraint.MaxWidth, constraint.MaxHeight);

    /// <summary>
    /// Override to arrange children. Call ArrangeChild for each child.
    /// </summary>
    public virtual void Arrange(Rect rect)
    {
    }

    /// <summary>
    /// Arrange a child at the given rect and store it for rendering in Draw.
    /// </summary>
    protected void ArrangeChild(IWidget child, Rect rect)
    {
        child.GetRenderer().Arrange(rect);
        _childPlacements.Add(new(child, rect));
    }

    public virtual IWidget? HitTest(int x, int y) => this;

    public abstract void Render(Canvas canvas);

    sealed class TestWidgetBaseScribe(TestWidgetBase owner) : IWidgetScribe
    {
        public void Draw(Canvas canvas)
        {
            owner.Render(canvas);
            for (var i = 0; i < owner._childPlacements.Count; i++)
            {
                var placement = owner._childPlacements[i];
                canvas.RenderChild(owner, in placement);
            }
        }
    }

    sealed class test_widget_renderer(TestWidgetBase owner) : IWidgetRenderer
    {
        public Size Measure(SizeConstraint constraint) => owner.Measure(constraint);

        public void Arrange(Rect rect)
        {
            owner._childPlacements.Clear();
            owner.Arrange(rect);
        }

        public void Draw(Canvas canvas) => owner.Render(canvas);
    }
}
