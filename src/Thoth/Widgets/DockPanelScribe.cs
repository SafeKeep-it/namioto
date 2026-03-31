using Thoth.Rendering;
using Thoth.Rendering.Layout;

namespace Thoth.Widgets;

public sealed class DockPanelScribe : IWidgetRenderer, IWidgetScribe
{
    readonly DockPanel _widget;
    readonly List<Canvas.ChildPlacement> _childPlacements = [];

    public DockPanelScribe(DockPanel widget)
    {
        _widget = widget;
    }

    public Size Measure(SizeConstraint constraint)
    {
        return new(constraint.MaxWidth, constraint.MaxHeight);
    }

    public void Arrange(Rect rect)
    {
        _childPlacements.Clear();
        var remainingRect = new Rect(0, 0, rect.Width, rect.Height);
        List<Dock>? fillWidgets = null;

        foreach (var child in _widget.Children)
        {
            if (child is not Dock dock) continue;

            if (dock.Position == DockPosition.Top)
            {
                var dockConstraint = new SizeConstraint(remainingRect.Width, remainingRect.Height);
                var height = dock.GetRenderer().Measure(dockConstraint).Height;
                height = Math.Min(height, remainingRect.Height);

                if (height > 0)
                {
                    var childRect = new Rect(remainingRect.X, remainingRect.Y, remainingRect.Width, height);
                    dock.GetRenderer().Arrange(childRect);
                    _childPlacements.Add(new(dock, childRect));
                    remainingRect = new(remainingRect.X, remainingRect.Y + height, remainingRect.Width, remainingRect.Height - height);
                }
            }
            else if (dock.Position == DockPosition.Bottom)
            {
                var dockConstraint = new SizeConstraint(remainingRect.Width, remainingRect.Height);
                var height = dock.GetRenderer().Measure(dockConstraint).Height;
                height = Math.Min(height, remainingRect.Height);

                if (height > 0)
                {
                    var childRect = new Rect(remainingRect.X, remainingRect.Y + remainingRect.Height - height, remainingRect.Width, height);
                    dock.GetRenderer().Arrange(childRect);
                    _childPlacements.Add(new(dock, childRect));
                    remainingRect = new(remainingRect.X, remainingRect.Y, remainingRect.Width, remainingRect.Height - height);
                }
            }
            else
            {
                fillWidgets ??= [];
                fillWidgets.Add(dock);
            }

            if (remainingRect.Height <= 0) break;
        }

        if (remainingRect.Height > 0 && fillWidgets != null)
        {
            foreach (var dock in fillWidgets)
            {
                dock.GetRenderer().Arrange(remainingRect);
                _childPlacements.Add(new(dock, remainingRect));
            }
        }
    }

    public void Draw(Canvas canvas)
    {
        for (var i = 0; i < _childPlacements.Count; i++)
        {
            var placement = _childPlacements[i];
            canvas.RenderChild(_widget, in placement);
        }
    }
}
