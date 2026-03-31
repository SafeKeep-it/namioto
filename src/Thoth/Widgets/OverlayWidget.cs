using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Rendering;
using Thoth.Rendering.Layout;

namespace Thoth.Widgets;

public sealed class OverlayWidget : IWidget,
                                   IWidgetWithLayout,
                                   IOverlayHost,
                                   IHandleCommand<OverlayWidget.ShowOverlayCommand>,
                                   IHandleCommand<OverlayWidget.CloseOverlayCommand>
{
    readonly OverlayWidgetScribe _scribe;
    internal readonly OverlayBackdrop _backdrop;
    IWidget? _activeOverlay;

    public OverlayWidget()
    {
        _scribe = new(this);
        _backdrop = new();
        _backdrop.Parent = this;
    }

    public required IWidget Content
    {
        get;
        init
        {
            field = value;
            value.Parent = this;
        }
    }

    public IWidget? ActiveOverlay
    {
        get => _activeOverlay;
        private set
        {
            if (ReferenceEquals(_activeOverlay, value)) return;
            if (_activeOverlay is not null) _activeOverlay.Parent = SentinelWidget.Instance;
            _activeOverlay = value;
            if (_activeOverlay is not null) _activeOverlay.Parent = this;
        }
    }

    public IWidget Parent { get; set; } = SentinelWidget.Instance;

    public IWidgetRenderer GetRenderer() => _scribe;

    public IWidgetScribe GetScribe() => _scribe;

    public Thoth.Widgets.Layout.ILayoutCreator GetLayoutCreator() => new OverlayWidgetLayout();

    public readonly record struct ShowOverlayCommand(IWidget Overlay);

    public readonly record struct CloseOverlayCommand;

    public void Show(IWidget overlay)
    {
        RenderPhaseGuard.ThrowIfActive("OverlayWidget.Show");
        ActiveOverlay = overlay;
    }

    public void Close()
    {
        RenderPhaseGuard.ThrowIfActive("OverlayWidget.Close");
        ActiveOverlay = null;
    }

    void IHandleCommand<ShowOverlayCommand>.Handle(ICommandContext context, in ShowOverlayCommand command)
    {
        _ = context;
        Show(command.Overlay);
    }

    void IHandleCommand<CloseOverlayCommand>.Handle(ICommandContext context, in CloseOverlayCommand command)
    {
        _ = context;
        _ = command;
        Close();
    }

    public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
    {
        if (!visitor.Visit(Content)) return;
        if (ActiveOverlay is null) return;
        if (!visitor.Visit(_backdrop)) return;
        visitor.Visit(ActiveOverlay);
    }

    public void Accept<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IVisitor, allows ref struct
    {
        visitor.Visit((IWidgetWithLayout)Content);
        visitor.Visit(_backdrop);
        if (ActiveOverlay is not null)
            visitor.Visit((IWidgetWithLayout)ActiveOverlay);
    }

    internal sealed class OverlayBackdrop : IWidget,
                                            IWidgetWithLayout,
                                            IEventHandler<OnMouseDown>,
                                            IEventHandler<OnMouseUp>,
                                            IEventHandler<OnMouseMove>,
                                            IEventHandler<OnMouseClick>,
                                            IEventHandler<MouseScrollEvent>
    {
        readonly BackdropScribe _scribe;

        public OverlayBackdrop()
        {
            _scribe = new(this);
        }

        public IWidget Parent { get; set; } = SentinelWidget.Instance;

        public IWidgetRenderer GetRenderer() => _scribe;

        public IWidgetScribe GetScribe() => _scribe;

        public Thoth.Widgets.Layout.ILayoutCreator GetLayoutCreator() => new OverlayBackdropLayout();

        public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
        {
            _ = visitor;
        }

        public void Accept<TVisitor>(ref TVisitor visitor)
            where TVisitor : struct, IVisitor, allows ref struct
        {
            _ = visitor;
        }

        void IEventHandler<OnMouseDown>.Handle(IEventContext context, in OnMouseDown e)
        {
            _ = e;
            context.IsHandled = true;
        }

        void IEventHandler<OnMouseUp>.Handle(IEventContext context, in OnMouseUp e)
        {
            _ = e;
            context.IsHandled = true;
        }

        void IEventHandler<OnMouseMove>.Handle(IEventContext context, in OnMouseMove e)
        {
            _ = e;
            context.IsHandled = true;
        }

        void IEventHandler<OnMouseClick>.Handle(IEventContext context, in OnMouseClick e)
        {
            _ = e;
            context.IsHandled = true;
        }

        void IEventHandler<MouseScrollEvent>.Handle(IEventContext context, in MouseScrollEvent e)
        {
            _ = e;
            context.IsHandled = true;
        }

        sealed class BackdropScribe(OverlayBackdrop owner) : IWidgetRenderer, IWidgetScribe
        {
            public Size Measure(SizeConstraint constraint)
            {
                _ = owner;
                return new(constraint.MaxWidth, constraint.MaxHeight);
            }

            public void Arrange(Rect rect)
            {
                _ = owner;
                _ = rect;
            }

            public void Draw(Canvas canvas)
            {
                _ = owner;
                _ = canvas;
            }
        }
    }
}
