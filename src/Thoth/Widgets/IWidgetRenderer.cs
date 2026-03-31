using Thoth.Rendering;
using Thoth.Rendering.Layout;

namespace Thoth.Widgets;

public interface IWidgetRenderer
{
    Size Measure(SizeConstraint constraint);
    void Arrange(Rect rect);
    void Draw(Canvas canvas);
}
