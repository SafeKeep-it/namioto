using Thoth.Rendering;
using Thoth.Rendering.Layout;

namespace Thoth.Widgets.Layout;

public class Align : IWidget, IWidgetWithLayout
{
    readonly IWidgetScribe _scribe;
    readonly AlignRenderer _renderer;
    Canvas.ChildPlacement _contentPlacement;
    IWidget? _content;
    WidthSizeMode _widthSizeMode = WidthSizeMode.Fill;
    HeightSizeMode _heightSizeMode = HeightSizeMode.Content;

    public Align()
    {
        _renderer = new AlignRenderer(this);
        _scribe = new AlignScribe(this);
        WidthSizeMode = WidthSizeMode.Fill;
        _contentPlacement = default;
    }

    public WidthSizeMode WidthSizeMode
    {
        get => _widthSizeMode;
        set
        {
            if (_widthSizeMode == value) return;
            _widthSizeMode = value;
            _renderer.ClearMeasuredCache();
        }
    }

    public HeightSizeMode HeightSizeMode
    {
        get => _heightSizeMode;
        set
        {
            if (_heightSizeMode == value) return;
            _heightSizeMode = value;
            _renderer.ClearMeasuredCache();
        }
    }

    public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;
    public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Top;

    public IWidget? Content
    {
        get => _content;
        set
        {
            RenderPhaseGuard.ThrowIfActive("Align.Content set");
            if (_content != null)
            {
                _content.Parent = SentinelWidget.Instance;
            }

            _content = value;
            _renderer.ClearMeasuredCache();
            if (value != null)
                value.Parent = this;
        }
    }

    public IWidget Parent { get; set; } = SentinelWidget.Instance;

    public IWidgetRenderer GetRenderer() => _renderer;

    public Size Measure(SizeConstraint constraint) => GetRenderer().Measure(constraint);

    public void Arrange(Rect rect) => GetRenderer().Arrange(rect);

    public IWidgetScribe GetScribe() => _scribe;

    public ILayoutCreator GetLayoutCreator() => new AlignLayout();

    public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
    {
        if (_content != null) visitor.Visit(_content);
    }

    public void Accept<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IVisitor, allows ref struct
    {
        if (_content is null) return;
        visitor.Visit((IWidgetWithLayout)_content);
    }

    sealed class AlignScribe(Align owner) : IWidgetScribe
    {
        public void Draw(Canvas canvas)
        {
            owner._renderer.Draw(canvas);
        }
    }

    sealed class AlignRenderer(Align widget) : IWidgetRenderer
    {
        mad_state _mad;

        public Size Measure(SizeConstraint constraint) => Measure(ref _mad, widget, constraint);

        public void Arrange(Rect rect) => Arrange(ref _mad, widget, rect);

        public void Draw(Canvas canvas) => Draw(ref _mad, widget, canvas);

        public void ClearMeasuredCache() => ClearMeasuredCache(ref _mad);

        public Size MeasureContent(SizeConstraint constraint) =>
            MeasureContent(ref _mad, widget, constraint);

        static Size Measure(ref mad_state state, Align widget, SizeConstraint constraint)
        {
            if (widget.Content == null) return new(0, 0);

            var size = MeasureContent(ref state, widget, constraint);
            var width = widget.WidthSizeMode == WidthSizeMode.Fill ? constraint.MaxWidth : size.Width;
            var height = widget.HeightSizeMode == HeightSizeMode.Fill ? constraint.MaxHeight : size.Height;
            return new(Math.Min(width, constraint.MaxWidth), Math.Min(height, constraint.MaxHeight));
        }

        static void Arrange(ref mad_state state, Align widget, Rect rect)
        {
            if (widget.Content == null) return;

            var size = MeasureContent(ref state, widget, new(rect.Width, rect.Height));
            var childWidth = Math.Min(size.Width, rect.Width);
            var childHeight = Math.Min(size.Height, rect.Height);

            var x = widget.HorizontalAlignment switch
            {
                HorizontalAlignment.Center => (rect.Width - childWidth) / 2,
                HorizontalAlignment.Right => rect.Width - childWidth,
                var _ => 0
            };
            var y = widget.VerticalAlignment switch
            {
                VerticalAlignment.Center => (rect.Height - childHeight) / 2,
                VerticalAlignment.Bottom => rect.Height - childHeight,
                var _ => 0
            };

            var childRect = new Rect(x, y, childWidth, childHeight);
            widget.Content.GetRenderer().Arrange(childRect);
            widget._contentPlacement = new(widget.Content, childRect);
        }

        static void Draw(ref mad_state state, Align widget, Canvas canvas)
        {
            _ = state;
            if (widget.Content is null) return;
            canvas.RenderChild(widget, in widget._contentPlacement);
        }

        static void ClearMeasuredCache(ref mad_state state)
        {
            state.LastMeasuredConstraint = null;
            state.LastMeasuredContentSize = default;
        }

        static Size MeasureContent(ref mad_state state, Align widget, SizeConstraint constraint)
        {
            if (state.LastMeasuredConstraint == constraint)
                return state.LastMeasuredContentSize;

            if (widget.Content == null) return default;

            var measured = widget.Content.GetRenderer().Measure(constraint);
            state.LastMeasuredConstraint = constraint;
            state.LastMeasuredContentSize = measured;
            return measured;
        }

        struct mad_state
        {
            public SizeConstraint? LastMeasuredConstraint;
            public Size LastMeasuredContentSize;
        }
    }

    Size MeasureContent(SizeConstraint constraint) => _renderer.MeasureContent(constraint);
}
