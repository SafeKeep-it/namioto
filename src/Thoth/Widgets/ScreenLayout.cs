using System.Text;
using Thoth.Rendering;
using Thoth.Rendering.Layout;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public class ScreenLayout : ILayoutCreator
{
    public WidgetSizeRequest Measure(in IWidgetWithLayout widget,
                                     in SizeConstraint constraint,
                                     ReadOnlySpan<WidgetSizeRequest> desires)
    {
        _ = widget as Screen
            ?? throw new InvalidOperationException($"{nameof(ScreenLayout)} requires {nameof(Screen)}.");
        _ = desires;
        return new(widget, this, new Size(constraint.MaxWidth, constraint.MaxHeight));
    }

    public void Arrange(in IWidgetWithLayout widget, in WidgetSize actual, ReadOnlySpan<WidgetSizeRequest> childDesires, Span<WidgetSize> children)
    {
        var screen = widget as Screen
            ?? throw new InvalidOperationException($"{nameof(ScreenLayout)} requires {nameof(Screen)}.");

        for (var i = 0; i < screen.Children.Count; i++)
        {
            var child = (IWidgetWithLayout)screen.Children[i];
            children[i] = new WidgetSize(child,
                                         child.GetLayoutCreator(),
                                         new Rect(0, 0, actual.Rect.Width, actual.Rect.Height));
        }
    }

    public void Draw(in IWidgetWithLayout widget, in Canvas canvas)
    {
        var screen = widget as Screen
            ?? throw new InvalidOperationException($"{nameof(ScreenLayout)} requires {nameof(Screen)}.");

        var styleIndex = canvas.Context.Styles.Intern(screen.Style);
        var spaceGlyph = canvas.PrepareRune((Rune)' ');
        canvas.FillPreparedGlyph(0, 0, canvas.Width, canvas.Height, spaceGlyph, styleIndex);
    }
}
