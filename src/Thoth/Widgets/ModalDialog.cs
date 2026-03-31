using System.Text;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Rendering;
using Thoth.Rendering.Layout;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public sealed class ModalDialog : IWidget,
                                   IWidgetWithLayout,
                                   IEventHandler<KeyPressedInput>
{
    static readonly Color close_background = new(196, 52, 61);
    static readonly Style default_header_style = new(Attributes: TextAttributes.Bold);

    readonly ModalDialogScribe _scribe;
    internal readonly Border Chrome;
    readonly DockPanel _layout;
    readonly Dock _headerDock;
    readonly ModalHeader _header;
    readonly Dock _footerDock;
    Dock? _contentDock;
    IWidget _content = SentinelWidget.Instance;
    bool _isFooterVisible = true;
    bool _isFooterAttached;
    bool _mandatory = true;
    string _title = string.Empty;
    Style _style = new();
    Style _headerStyle = default_header_style;
    Color _closeButtonBackgroundColor = close_background;
    Color _closeButtonForegroundColor = Color.White;

    public ModalDialog()
    {
        FooterButtons = new ButtonGroup();
        _layout = new DockPanel();

        _header = new(() => OnDismiss?.Invoke());
        _headerDock = new()
                      {
                          Position = DockPosition.Top,
                          Content = _header,
                          MaximumHeight = 1
                      };
        _layout.Add(_headerDock);

        _footerDock = new()
                      {
                          Position = DockPosition.Bottom,
                          Content = FooterButtons
                      };
        _layout.Add(_footerDock);
        _isFooterAttached = true;

        Chrome = new Border
                 {
                     BorderStyle = BorderStyle.Inset,
                     Content = _layout,
                     Style = _style
                 };
        Chrome.Parent = this;

        _scribe = new(this);
        sync_header();
    }

    public IWidget Parent { get; set; } = SentinelWidget.Instance;

    public int Width { get; set; } = 40;

    public int Height { get; set; } = 10;

    public Style Style
    {
        get => _style;
        set
        {
            _style = value;
            Chrome.Style = value;
        }
    }

    public double MaxWidthRatio { get; set; } = 1.0;

    public double MaxHeightRatio { get; set; } = 2.0 / 3.0;

    public string Title
    {
        get => _title;
        set
        {
            _title = value ?? string.Empty;
            sync_header();
        }
    }

    public bool Mandatory
    {
        get => _mandatory;
        set
        {
            if (_mandatory == value) return;
            _mandatory = value;
            sync_header();
        }
    }

    public bool FooterVisible
    {
        get => _isFooterVisible;
        set
        {
            if (_isFooterVisible == value) return;
            _isFooterVisible = value;
            sync_footer_visibility();
        }
    }

    public Action? OnDismiss { get; set; }

    public ButtonGroup FooterButtons { get; }

    public Style HeaderStyle
    {
        get => _headerStyle;
        set
        {
            _headerStyle = value;
            sync_header();
        }
    }

    public Color CloseButtonBackgroundColor
    {
        get => _closeButtonBackgroundColor;
        set
        {
            _closeButtonBackgroundColor = value;
            sync_header();
        }
    }

    public Color CloseButtonForegroundColor
    {
        get => _closeButtonForegroundColor;
        set
        {
            _closeButtonForegroundColor = value;
            sync_header();
        }
    }

    public required IWidget Content
    {
        get => _content;
        init
        {
            _content = value;
            if (_contentDock is not null)
                _layout.Remove(_contentDock);

            _contentDock = new Dock
                           {
                               Position = DockPosition.Fill,
                               Content = value
                           };
            _layout.Add(_contentDock);
        }
    }

    void sync_footer_visibility()
    {
        if (_isFooterVisible && !_isFooterAttached)
        {
            _layout.Add(_footerDock);
            _isFooterAttached = true;
            return;
        }

        if (!_isFooterVisible && _isFooterAttached)
        {
            _layout.Remove(_footerDock);
            _isFooterAttached = false;
        }
    }

    void sync_header()
    {
        _header.Title = string.IsNullOrWhiteSpace(_title) ? null : _title;
        _header.ShowClose = !_mandatory;
        _header.HeaderStyle = _headerStyle;
        _header.CloseButtonBackgroundColor = _closeButtonBackgroundColor;
        _header.CloseButtonForegroundColor = _closeButtonForegroundColor;
    }

    public IWidgetRenderer GetRenderer() => _scribe;

    public IWidgetScribe GetScribe() => _scribe;

    public ILayoutCreator GetLayoutCreator() => new ModalDialogLayout();

    public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
    {
        visitor.Visit(Chrome);
    }

    public void Accept<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IVisitor, allows ref struct
    {
        visitor.Visit(Chrome);
    }

    void IEventHandler<KeyPressedInput>.Handle(IEventContext context, in KeyPressedInput e)
    {
        if (e.Key.Key != ConsoleKey.Escape) return;

        if (Mandatory) return;

        OnDismiss?.Invoke();
        context.IsHandled = true;
    }

    internal sealed class ModalHeader : IWidget,
                                        IWidgetWithLayout
    {
        readonly modal_header_scribe _scribe;
        internal readonly TextBar _title_bar;
        internal readonly Button _close_button;
        internal bool _showClose;

        public ModalHeader(Action close)
        {
            _title_bar = new()
                         {
                             Line = " ",
                             Style = new(Attributes: TextAttributes.Bold)
                         };

            _close_button = new()
                            {
                                Text = "×",
                                MinWidth = 1,
                                BorderStyle = BorderStyle.None,
                                Style = new(Color.White, close_background),
                                ForegroundColor = Color.White,
                                BackgroundColor = close_background,
                                OnClick = close
                            };
            _title_bar.Parent = this;
            _close_button.Parent = this;

            _scribe = new(this);
        }

        public IWidget Parent { get; set; } = SentinelWidget.Instance;

        public string? Title
        {
            get => _title_bar.CenterTitle;
            set => _title_bar.CenterTitle = value;
        }

        public bool ShowClose
        {
            get => _showClose;
            set => _showClose = value;
        }

        public Style HeaderStyle
        {
            get => _title_bar.Style;
            set => _title_bar.Style = value;
        }

        public Color CloseButtonBackgroundColor
        {
            get => _close_button.BackgroundColor ?? close_background;
            set
            {
                _close_button.BackgroundColor = value;
                _close_button.Style = _close_button.Style with { Background = value };
            }
        }

        public Color CloseButtonForegroundColor
        {
            get => _close_button.ForegroundColor ?? Color.White;
            set
            {
                _close_button.ForegroundColor = value;
                _close_button.Style = _close_button.Style with { Foreground = value };
            }
        }

        public IWidgetRenderer GetRenderer() => _scribe;

        public IWidgetScribe GetScribe() => _scribe;

        public ILayoutCreator GetLayoutCreator() => new ModalDialogHeaderLayout();

        public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
        {
            if (!visitor.Visit(_title_bar)) return;
            if (_showClose) visitor.Visit(_close_button);
        }

        public void Accept<TVisitor>(ref TVisitor visitor)
            where TVisitor : struct, IVisitor, allows ref struct
        {
            if (_title_bar is IWidgetWithLayout titleBar)
                visitor.Visit(titleBar);

            if (_showClose && _close_button is IWidgetWithLayout closeButton)
                visitor.Visit(closeButton);
        }

        sealed class modal_header_scribe(ModalHeader owner) : IWidgetRenderer, IWidgetScribe
        {
            Canvas.ChildPlacement _titleBarPlacement;
            Canvas.ChildPlacement _closeButtonPlacement;
            bool _hasCloseButton;

            public Size Measure(SizeConstraint constraint)
            {
                return new(constraint.MaxWidth, Math.Min(1, constraint.MaxHeight));
            }

            public void Arrange(Rect rect)
            {
                var height = Math.Min(1, rect.Height);
                owner._title_bar.GetRenderer().Arrange(new(0, 0, rect.Width, height));
                _titleBarPlacement = new(owner._title_bar, new(0, 0, rect.Width, height));

                if (!owner._showClose)
                {
                    _hasCloseButton = false;
                    return;
                }

                var closeSize = owner._close_button.GetRenderer().Measure(new(rect.Width, height));
                var closeWidth = Math.Max(1, Math.Min(closeSize.Width, rect.Width));
                var closeX = Math.Max(0, rect.Width - closeWidth);
                var closeRect = new Rect(closeX, 0, closeWidth, height);
                owner._close_button.GetRenderer().Arrange(closeRect);
                _closeButtonPlacement = new(owner._close_button, closeRect);
                _hasCloseButton = true;
            }

            public void Draw(Canvas canvas)
            {
                canvas.RenderChild(owner, in _titleBarPlacement);
                if (_hasCloseButton)
                    canvas.RenderChild(owner, in _closeButtonPlacement);
            }
        }

    }

    sealed class ModalDialogHeaderLayout : ILayoutCreator
    {
        public WidgetSizeRequest Measure(in IWidgetWithLayout widget,
                                         in SizeConstraint constraint,
                                         ReadOnlySpan<WidgetSizeRequest> desires)
        {
            var header = widget as ModalHeader
                ?? throw new InvalidOperationException($"{nameof(ModalDialogHeaderLayout)} requires {nameof(ModalHeader)}.");

            _ = desires;
            return new(header, this, new Size(constraint.MaxWidth, Math.Min(1, constraint.MaxHeight)));
        }

        public void Arrange(in IWidgetWithLayout widget, in WidgetSize actual, ReadOnlySpan<WidgetSizeRequest> childDesires, Span<WidgetSize> children)
        {
            var header = widget as ModalHeader
                ?? throw new InvalidOperationException($"{nameof(ModalDialogHeaderLayout)} requires {nameof(ModalHeader)}.");

            _ = childDesires;
            var height = Math.Min(1, actual.Rect.Height);
            var childIndex = 0;

            if (header._title_bar is IWidgetWithLayout titleBar && childIndex < children.Length)
            {
                children[childIndex++] = new WidgetSize(titleBar,
                                                        titleBar.GetLayoutCreator(),
                                                        new Rect(0, 0, actual.Rect.Width, height));
            }

            if (!header._showClose || header._close_button is not IWidgetWithLayout closeButton || childIndex >= children.Length)
                return;

            var closeDesired = closeButton.GetLayoutCreator().Measure(closeButton,
                                                                     new SizeConstraint(actual.Rect.Width, height),
                                                                     ReadOnlySpan<WidgetSizeRequest>.Empty);
            var closeWidth = Math.Max(1, Math.Min(closeDesired.Size.Width, actual.Rect.Width));
            var closeX = Math.Max(0, actual.Rect.Width - closeWidth);
            children[childIndex] = new WidgetSize(closeButton,
                                                  closeButton.GetLayoutCreator(),
                                                  new Rect(closeX, 0, closeWidth, height));
        }

        public void Draw(in IWidgetWithLayout widget, in Canvas canvas)
        {
            var header = widget as ModalHeader
                ?? throw new InvalidOperationException($"{nameof(ModalDialogHeaderLayout)} requires {nameof(ModalHeader)}.");

            if (!header._showClose || header._close_button is IWidgetWithLayout)
                return;

            if (canvas.Width <= 0 || canvas.Height <= 0)
                return;

            var styleIndex = canvas.Context.Styles.Intern(new Style(header.CloseButtonForegroundColor,
                                                                    header.CloseButtonBackgroundColor));
            var background = canvas.PrepareRune((Rune)' ');
            var glyph = canvas.PrepareRune((Rune)'×');
            var x = canvas.Width - 1;
            canvas.FillPreparedGlyph(x, 0, 1, canvas.Height, background, styleIndex);
            canvas.PutPreparedGlyph(x, 0, glyph, styleIndex);
        }
    }
}

public sealed class ModalDialogLayout : ILayoutCreator
{
    public WidgetSizeRequest Measure(in IWidgetWithLayout widget,
                                     in SizeConstraint constraint,
                                     ReadOnlySpan<WidgetSizeRequest> desires)
    {
        var dialog = widget as ModalDialog
            ?? throw new InvalidOperationException($"{nameof(ModalDialogLayout)} requires {nameof(ModalDialog)}.");

        _ = desires;
        var width = ClampDimension(dialog.Width, constraint.MaxWidth, dialog.MaxWidthRatio);
        var height = ClampDimension(dialog.Height, constraint.MaxHeight, dialog.MaxHeightRatio);
        return new(dialog, this, new Size(width, height));
    }

    public void Arrange(in IWidgetWithLayout widget, in WidgetSize actual, ReadOnlySpan<WidgetSizeRequest> childDesires, Span<WidgetSize> children)
    {
        var dialog = widget as ModalDialog
            ?? throw new InvalidOperationException($"{nameof(ModalDialogLayout)} requires {nameof(ModalDialog)}.");

        _ = childDesires;
        if (children.Length == 0)
            return;

        var width = ClampDimension(dialog.Width, actual.Rect.Width, dialog.MaxWidthRatio);
        var height = ClampDimension(dialog.Height, actual.Rect.Height, dialog.MaxHeightRatio);
        var x = Math.Max(0, (actual.Rect.Width - width) / 2);
        var y = Math.Max(0, (actual.Rect.Height - height) / 2);

        children[0] = new WidgetSize(dialog.Chrome,
                                     dialog.Chrome.GetLayoutCreator(),
                                     new Rect(x, y, width, height));
    }

    public void Draw(in IWidgetWithLayout widget, in Canvas canvas)
    {
        _ = widget;
        _ = canvas;
    }

    static int ClampDimension(int requested, int available, double ratio)
    {
        var normalizedRatio = Math.Clamp(ratio, 0.0, 1.0);
        var ratioLimit = Math.Max(1, (int)Math.Floor(available * normalizedRatio));
        var maxDimension = Math.Min(available, ratioLimit);
        return Math.Min(Math.Max(1, requested), maxDimension);
    }
}
