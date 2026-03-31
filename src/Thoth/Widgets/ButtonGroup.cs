using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Navigation.Focus;
using Thoth.Rendering;
using Thoth.Rendering.Layout;

namespace Thoth.Widgets;

public sealed class ButtonGroup : IWidget,
                                  IWidgetWithLayout,
                                  IFocusable,
                                  IEventHandler<OnFocus>,
                                  IEventHandler<KeyPressedInput>
{
    readonly Dictionary<Button, Color?> _baseBorderColors = new(ReferenceEqualityComparer.Instance);
    readonly List<Button> _buttons = [];
    readonly List<Button> _orderedButtons = [];
    readonly ButtonGroupScribe _scribe;
    bool _orderedDirty = true;
    Button? _defaultButton;

    public ButtonGroup()
    {
        _scribe = new(this);
    }

    public int ButtonGap { get; set; } = 1;

    public Color? SelectedBorderColor { get; set; }

    public Button? DefaultButton
    {
        get => _defaultButton;
        set
        {
            if (ReferenceEquals(_defaultButton, value)) return;
            _defaultButton = value;
            _orderedDirty = true;
        }
    }

    public Button? SelectedButton { get; set; }

    void IEventHandler<KeyPressedInput>.Handle(IEventContext context, in KeyPressedInput e)
    {
        EnsureSelection();

        if (e.Key.Key == ConsoleKey.Enter)
        {
            Activate(DefaultButton ?? SelectedButton);
            context.IsHandled = true;
            return;
        }

        if (e.Key.Key == ConsoleKey.Spacebar)
        {
            Activate(SelectedButton ?? DefaultButton);
            context.IsHandled = true;
            return;
        }

        if (e.Key.Key == ConsoleKey.LeftArrow)
        {
            MoveSelection(-1);
            context.RaiseEvent(new OnContentChanged());
            context.IsHandled = true;
            return;
        }

        if (e.Key.Key == ConsoleKey.RightArrow)
        {
            MoveSelection(1);
            context.RaiseEvent(new OnContentChanged());
            context.IsHandled = true;
            return;
        }

        if (e.Key.Key == ConsoleKey.Tab)
        {
            var delta = (e.Key.Modifiers & ConsoleModifiers.Shift) != 0 ? -1 : 1;
            MoveSelection(delta);
            context.RaiseEvent(new OnContentChanged());
            context.IsHandled = true;
        }
    }

    void IEventHandler<OnFocus>.Handle(IEventContext context, in OnFocus e)
    {
        _ = e;
        EnsureSelection();
        context.IsHandled = true;
    }

    public IWidget Parent { get; set; } = SentinelWidget.Instance;

    public IWidgetRenderer GetRenderer() => _scribe;

    public IWidgetScribe GetScribe() => _scribe;

    public Thoth.Widgets.Layout.ILayoutCreator GetLayoutCreator() => new ButtonGroupLayout();

    internal int ButtonCount => _buttons.Count;

    public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
    {
        var ordered = EnsureOrdered();
        for (var i = 0; i < ordered.Count; i++)
        {
            if (!visitor.Visit(ordered[i])) return;
        }
    }

    public void Accept<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IVisitor, allows ref struct
    {
        var ordered = EnsureOrdered();
        for (var i = 0; i < ordered.Count; i++)
            visitor.Visit((IWidgetWithLayout)ordered[i]);
    }

    public void Add(Button button)
    {
        RenderPhaseGuard.ThrowIfActive("ButtonGroup.Add");
        if (Contains(button)) return;
        button.Parent = this;
        _buttons.Add(button);
        _baseBorderColors[button] = button.BorderColor;
        _orderedDirty = true;
        _scribe.InvalidateMeasured();
    }

    bool Contains(Button button)
    {
        for (var i = 0; i < _buttons.Count; i++)
        {
            if (ReferenceEquals(_buttons[i], button)) return true;
        }

        return false;
    }

    internal void EnsureSelection()
    {
        if (SelectedButton is not null && Contains(SelectedButton)) return;
        if (DefaultButton is not null && Contains(DefaultButton))
        {
            SelectedButton = DefaultButton;
            return;
        }

        SelectedButton = _buttons.Count > 0 ? _buttons[0] : null;
    }

    void Activate(Button? button)
    {
        button?.Activate();
    }

    void MoveSelection(int delta)
    {
        if (_buttons.Count == 0 || delta == 0) return;
        EnsureSelection();
        if (SelectedButton is null) return;

        var ordered = EnsureOrdered();
        var index = IndexOf(ordered, SelectedButton);
        if (index < 0) return;

        var next = (index + delta + ordered.Count) % ordered.Count;
        SelectedButton = ordered[next];
    }

    internal List<Button> EnsureOrdered()
    {
        if (!_orderedDirty) return _orderedButtons;

        _orderedButtons.Clear();
        for (var i = 0; i < _buttons.Count; i++)
        {
            var button = _buttons[i];
            if (_defaultButton is not null && ReferenceEquals(button, _defaultButton)) continue;
            _orderedButtons.Add(button);
        }

        if (_defaultButton is not null && Contains(_defaultButton)) _orderedButtons.Add(_defaultButton);

        _orderedDirty = false;
        return _orderedButtons;
    }

    static int IndexOf(List<Button> items, Button target)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i], target)) return i;
        }

        return -1;
    }

    internal void ApplyVisualSelection()
    {
        for (var i = 0; i < _buttons.Count; i++)
        {
            var button = _buttons[i];
            _baseBorderColors.TryGetValue(button, out var baseColor);

            if (SelectedButton is not null && ReferenceEquals(button, SelectedButton))
                button.BorderColor = SelectedBorderColor ?? baseColor;
            else
                button.BorderColor = baseColor;
        }
    }

}
