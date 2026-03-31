using System.Text;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Modal;
using Thoth.Navigation.Focus;
using Thoth.Rendering;
using Thoth.Rendering.Layout;
using Thoth.Widgets.Layout;
using ThemeRegistry = Thoth.Themes.Themes;
using Thoth.Themes;

namespace Thoth.Widgets;

public sealed class SingleChoiceList : IWidget,
                                       IWidgetWithLayout,
                                       IFocusable,
                                       IEventHandler<OnFocus>,
                                       IEventHandler<KeyPressedInput>
{
    static readonly Color fallback_active_background = new(226, 237, 252);
    static readonly Color default_checked_foreground = new(46, 160, 67);

    readonly SingleChoiceListScribe _scribe;
    internal readonly Table Table;
    readonly List<choice_row> _rows = [];
    readonly Color _defaultCheckedForeground;
    int _activeRowIndex;

    public SingleChoiceList()
    {
        var variant = resolve_choice_variant();

        Table = new Table();
        Table.AddAutoColumn();
        Table.AddAutoColumn();
        Table.AddFillColumn();
        Table.Parent = this;

        ActiveRowBackgroundColor = variant.ActiveRowBackground;
        _defaultCheckedForeground = variant.CheckedForeground;

        _scribe = new(this);
    }

    public IWidget Parent { get; set; } = SentinelWidget.Instance;

    public Color? ActiveRowBackgroundColor { get; set; }

    public Color? RowBackgroundColor { get; set; }

    public Color? ActiveRowForegroundColor { get; set; }

    public Color? RowForegroundColor { get; set; }

    public Color? CheckedForegroundColor { get; set; }

    public string? SelectedChoiceId
    {
        get
        {
            for (var i = 0; i < _rows.Count; i++)
                if (_rows[i].Toggle.IsChecked)
                    return _rows[i].Id;

            return null;
        }
    }

    public IReadOnlyList<string> SelectedChoiceIds
    {
        get
        {
            var selectedId = SelectedChoiceId;
            return selectedId is null ? [] : [selectedId];
        }
    }

    public IWidgetRenderer GetRenderer() => _scribe;

    public IWidgetScribe GetScribe() => _scribe;

    public ILayoutCreator GetLayoutCreator() => new SingleChoiceListLayout();

    public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
    {
        visitor.Visit(Table);
    }

    public void Accept<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IVisitor, allows ref struct
    {
        var table = Table as object as IWidgetWithLayout;
        if (table is not null)
            visitor.Visit(table);
    }

    public void SetChoices(IReadOnlyList<ModalDialogChoice> choices)
    {
        RenderPhaseGuard.ThrowIfActive("SingleChoiceList.SetChoices");

        _rows.Clear();
        Table.ClearRows();

        var selectedFound = false;
        for (var i = 0; i < choices.Count; i++)
        {
            var choice = choices[i];
            var isChecked = choice.IsChecked && !selectedFound;
            if (isChecked) selectedFound = true;

            var toggle = new Toggle
                         {
                             IsChecked = isChecked,
                             UncheckedGlyph = (Rune)'○',
                             CheckedGlyph = (Rune)'◉',
                             CheckedForegroundColor = CheckedForegroundColor
                         };
            var label = new TextBlock
                        {
                            Text = choice.Label,
                            Overflow = TextOverflow.Wrap
                        };
            var gap = new GapCell();

            Table.AddRow(toggle, gap, label);
            _rows.Add(new(choice.Id,
                          toggle,
                          gap,
                          label,
                          toggle.BackgroundColor,
                          gap.BackgroundColor,
                          label.BackgroundColor,
                          toggle.CheckedForegroundColor,
                          toggle.UncheckedForegroundColor,
                          label.ForegroundColor));
        }

        _activeRowIndex = Math.Clamp(_activeRowIndex, 0, Math.Max(0, _rows.Count - 1));
        ApplyActiveRowStyles();
    }

    void IEventHandler<OnFocus>.Handle(IEventContext context, in OnFocus e)
    {
        _ = e;
        EnsureActiveRow();
        ApplyActiveRowStyles();
        context.RaiseEvent(new OnContentChanged());
        context.IsHandled = true;
    }

    void IEventHandler<KeyPressedInput>.Handle(IEventContext context, in KeyPressedInput e)
    {
        EnsureActiveRow();

        if (e.Key.Key == ConsoleKey.UpArrow)
        {
            MoveActiveRow(-1);
            ApplyActiveRowStyles();
            context.RaiseEvent(new OnContentChanged());
            context.IsHandled = true;
            return;
        }

        if (e.Key.Key == ConsoleKey.DownArrow)
        {
            MoveActiveRow(1);
            ApplyActiveRowStyles();
            context.RaiseEvent(new OnContentChanged());
            context.IsHandled = true;
            return;
        }

        if (e.Key.Key == ConsoleKey.Spacebar)
        {
            if (_rows.Count == 0) return;

            for (var i = 0; i < _rows.Count; i++)
                _rows[i].Toggle.IsChecked = i == _activeRowIndex;

            ApplyActiveRowStyles();
            context.RaiseEvent(new OnContentChanged());
            context.IsHandled = true;
        }
    }

    void EnsureActiveRow()
    {
        if (_rows.Count == 0)
        {
            _activeRowIndex = 0;
            return;
        }

        _activeRowIndex = Math.Clamp(_activeRowIndex, 0, _rows.Count - 1);
    }

    void MoveActiveRow(int delta)
    {
        if (_rows.Count == 0 || delta == 0) return;
        var next = _activeRowIndex + delta;
        _activeRowIndex = Math.Clamp(next, 0, _rows.Count - 1);
    }

    internal void ApplyActiveRowStyles()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            var isActive = i == _activeRowIndex;
            var rowBackground = isActive ? ActiveRowBackgroundColor : RowBackgroundColor;
            var rowForeground = isActive
                ? (ActiveRowForegroundColor ?? RowForegroundColor)
                : RowForegroundColor;

            _rows[i].Toggle.BackgroundColor = rowBackground ?? _rows[i].BaseToggleBackgroundColor;
            _rows[i].Gap.BackgroundColor = rowBackground ?? _rows[i].BaseGapBackgroundColor;
            _rows[i].Label.BackgroundColor = rowBackground ?? _rows[i].BaseLabelBackgroundColor;

            _rows[i].Label.ForegroundColor = rowForeground ?? _rows[i].BaseLabelForegroundColor;
            _rows[i].Toggle.UncheckedForegroundColor = rowForeground ?? _rows[i].BaseUncheckedForegroundColor;
            _rows[i].Toggle.CheckedForegroundColor = CheckedForegroundColor
                                                     ?? rowForeground
                                                     ?? _rows[i].BaseCheckedForegroundColor
                                                     ?? _defaultCheckedForeground;
        }
    }

    static ChoiceListVariant resolve_choice_variant()
    {
        var theme = ThemeRegistry.Current;
        if (theme is null)
            return new(fallback_active_background,
                       Color.White,
                       fallback_active_background,
                       new Color(28, 40, 67),
                       default_checked_foreground);

        var palette = theme.BuildPalette();
        var isDark = string.Equals(theme.Variant, "dark", StringComparison.OrdinalIgnoreCase);
        return ThemeControlVariants.From(palette, isDark).ChoiceList;
    }

    sealed record choice_row(string Id,
                             Toggle Toggle,
                             GapCell Gap,
                             TextBlock Label,
                             Color? BaseToggleBackgroundColor,
                             Color? BaseGapBackgroundColor,
                             Color? BaseLabelBackgroundColor,
                             Color? BaseCheckedForegroundColor,
                             Color? BaseUncheckedForegroundColor,
                             Color? BaseLabelForegroundColor);

    sealed class GapCell : IWidget,
                           IWidgetWithLayout
    {
        readonly gap_scribe _scribe;

        public GapCell()
        {
            _scribe = new(this);
        }

        public IWidget Parent { get; set; } = SentinelWidget.Instance;

        public Color? BackgroundColor { get; set; }

        public IWidgetRenderer GetRenderer() => _scribe;

        public IWidgetScribe GetScribe() => _scribe;

        public ILayoutCreator GetLayoutCreator() => new GapCellLayout();

        public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
        {
        }

        public void Accept<TVisitor>(ref TVisitor visitor)
            where TVisitor : struct, IVisitor, allows ref struct
        {
        }

        sealed class gap_scribe(GapCell owner) : IWidgetRenderer, IWidgetScribe
        {
            public Size Measure(SizeConstraint constraint)
            {
                return new(Math.Min(1, constraint.MaxWidth), Math.Min(1, constraint.MaxHeight));
            }

            public void Arrange(Rect rect)
            {
                _ = rect;
            }

            public void Draw(Canvas canvas)
            {
                if (canvas.Width <= 0 || canvas.Height <= 0) return;
                var bg = new Style(null, owner.BackgroundColor);
                canvas.Fill(0, 0, 1, canvas.Height, (Rune)' ', bg);
            }
        }

        sealed class GapCellLayout : ILayoutCreator
        {
            public WidgetSizeRequest Measure(in IWidgetWithLayout widget,
                                             in SizeConstraint constraint,
                                             ReadOnlySpan<WidgetSizeRequest> requests)
            {
                var gap = widget as GapCell
                    ?? throw new InvalidOperationException($"{nameof(GapCellLayout)} requires {nameof(GapCell)}.");

                _ = requests;
                var size = new Size(Math.Min(1, constraint.MaxWidth), Math.Min(1, constraint.MaxHeight));
                return new(gap, this, size);
            }

            public void Arrange(in IWidgetWithLayout widget, in WidgetSize actual, ReadOnlySpan<WidgetSizeRequest> childRequests, Span<WidgetSize> children)
            {
                _ = widget;
                _ = actual;
                _ = childRequests;
                _ = children;
            }

            public void Draw(in IWidgetWithLayout widget, in Canvas canvas)
            {
                var gap = widget as GapCell
                    ?? throw new InvalidOperationException($"{nameof(GapCellLayout)} requires {nameof(GapCell)}.");

                if (canvas.Width <= 0 || canvas.Height <= 0) return;
                var styleIndex = canvas.Context.Styles.Intern(new Style(null, gap.BackgroundColor));
                var space = canvas.PrepareRune((Rune)' ');
                canvas.FillPreparedGlyph(0, 0, 1, canvas.Height, space, styleIndex);
            }
        }
    }
}

public sealed class SingleChoiceListLayout : ILayoutCreator
{
    public WidgetSizeRequest Measure(in IWidgetWithLayout widget,
                                     in SizeConstraint constraint,
                                     ReadOnlySpan<WidgetSizeRequest> requests)
    {
        var list = widget as SingleChoiceList
            ?? throw new InvalidOperationException($"{nameof(SingleChoiceListLayout)} requires {nameof(SingleChoiceList)}.");

        var size = requests.Length > 0
            ? requests[0].Size
            : list.Table.GetRenderer().Measure(constraint);
        return new(list, this, size);
    }

    public void Arrange(in IWidgetWithLayout widget, in WidgetSize actual, ReadOnlySpan<WidgetSizeRequest> childDesires, Span<WidgetSize> children)
    {
        var list = widget as SingleChoiceList
            ?? throw new InvalidOperationException($"{nameof(SingleChoiceListLayout)} requires {nameof(SingleChoiceList)}.");

        _ = childDesires;
        if (children.Length == 0) return;
        var table = list.Table as object as IWidgetWithLayout;
        if (table is null) return;

        children[0] = new WidgetSize(table,
                                     table.GetLayoutCreator(),
                                     new Rect(0, 0, actual.Rect.Width, actual.Rect.Height));
    }

    public void Draw(in IWidgetWithLayout widget, in Canvas canvas)
    {
        var list = widget as SingleChoiceList
            ?? throw new InvalidOperationException($"{nameof(SingleChoiceListLayout)} requires {nameof(SingleChoiceList)}.");
        list.ApplyActiveRowStyles();
    }
}
