using Thoth.Rendering;
using Thoth.Rendering.Layout;

namespace Thoth.Widgets;

public sealed class SentinelWidgetScribe : IWidgetRenderer, IWidgetScribe
{
    public Size Measure(SizeConstraint constraint)
    {
        _ = constraint;
        return new(0, 0);
    }

    public void Arrange(Rect rect)
    {
        _ = rect;
    }

    public void Draw(Canvas canvas)
    {
        _ = canvas;
    }
}
