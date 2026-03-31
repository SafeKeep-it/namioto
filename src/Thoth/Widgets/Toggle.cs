using System.Text;
using Thoth.Bindings;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Rendering;
using Thoth.Widgets.Layout;
using ThemeRegistry = Thoth.Themes.Themes;

namespace Thoth.Widgets;

public sealed class Toggle : IWidget,
                             IWidgetWithLayout,
                             IBindingWidget,
                             IEventHandler<OnMouseClick>
{
    static readonly Color fallback_checked_color = new(46, 160, 67);

    readonly ToggleScribe _scribe;
    Observable<bool> _isChecked = new();

    public Toggle()
    {
        _scribe = new(this);
        CheckedForegroundColor = ResolveCheckedForeground();
    }

    public IWidget Parent { get; set; } = SentinelWidget.Instance;

    public Observable<bool> IsChecked
    {
        get => _isChecked;
        set
        {
            if (ReferenceEquals(_isChecked, value)) return;

            _isChecked.Unbind(this);
            _isChecked = value.Bind(this);
        }
    }

    public Rune CheckedGlyph { get; set; } = (Rune)'☑';

    public Rune UncheckedGlyph { get; set; } = (Rune)'☐';

    public Color? CheckedForegroundColor { get; set; }

    public Color? UncheckedForegroundColor { get; set; }

    public Color? BackgroundColor { get; set; }

    public Action<bool>? OnChanged { get; set; }

    static Color ResolveCheckedForeground()
    {
        var theme = ThemeRegistry.Current;
        return theme?.BuildPalette().Success ?? fallback_checked_color;
    }

    public IWidgetRenderer GetRenderer() => _scribe;

    public IWidgetScribe GetScribe() => _scribe;

    public ILayoutCreator GetLayoutCreator() => new ToggleLayout();

    public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
    {
    }

    public void Accept<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IVisitor, allows ref struct
    {
    }

    public void ToggleChecked()
    {
        _isChecked.Set(!_isChecked);
    }

    void IEventHandler<OnMouseClick>.Handle(IEventContext context, in OnMouseClick e)
    {
        _ = e;
        ToggleChecked();
        OnChanged?.Invoke(_isChecked);
        context.RaiseEvent(new OnContentChanged());
        context.IsHandled = true;
    }

}
