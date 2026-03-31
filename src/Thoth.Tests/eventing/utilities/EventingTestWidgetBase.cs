using Thoth.Rendering;
using Thoth.Widgets;

namespace Thoth.Tests.eventing.utilities;

public abstract class EventingTestWidgetBase : IWidget
{
    readonly IWidgetScribe _scribe;
    readonly IWidgetRenderer _renderer;
    readonly List<IWidget> _children = [];
    Rect? _arrangedRect;

    protected EventingTestWidgetBase()
    {
        _renderer = new eventing_test_widget_renderer(this);
        _scribe = new EventingTestWidgetBaseScribe(this);
    }

    public IWidget Parent { get; set; } = SentinelWidget.Instance;

    public IWidgetRenderer GetRenderer() => _renderer;

    public IWidgetScribe GetScribe() => _scribe;

    public void AddChild(IWidget child)
    {
        child.Parent = this;
        _children.Add(child);
    }

    public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
    {
        for (var i = 0; i < _children.Count; i++)
        {
            if (!visitor.Visit(_children[i])) return;
        }
    }

    public virtual Size Measure(SizeConstraint constraint) => new(0, 0);

    public virtual void Arrange(Rect rect)
    {
        _arrangedRect = rect;
    }

    public virtual void Render(Canvas canvas)
    {
    }

    public virtual IWidget? HitTest(int x, int y)
    {
        if (_arrangedRect is not { } rect) return this;
        if (x < rect.X || x >= rect.X + rect.Width || y < rect.Y || y >= rect.Y + rect.Height) return null;
        return this;
    }

    sealed class EventingTestWidgetBaseScribe(EventingTestWidgetBase owner) : IWidgetScribe
    {
        public void Draw(Canvas canvas)
        {
            owner._renderer.Draw(canvas);
        }
    }

    sealed class eventing_test_widget_renderer(EventingTestWidgetBase owner) : IWidgetRenderer
    {
        public Size Measure(SizeConstraint constraint) => owner.Measure(constraint);

        public void Arrange(Rect rect)
        {
            owner.Arrange(rect);
        }

        public void Draw(Canvas canvas) => owner.Render(canvas);
    }
}
