using Thoth.Widgets;

namespace Thoth.Widgets.Layout;

/// <summary>
/// Tracks per-widget dirty state for the drawing pipeline.
/// null sets mean "all dirty" (full redraw, e.g. first frame).
/// </summary>
public readonly struct DirtyMap
{
    readonly HashSet<IWidgetWithLayout>? _layoutDirty;
    readonly HashSet<IWidgetWithLayout>? _contentDirty;

    internal DirtyMap(HashSet<IWidgetWithLayout>? layoutDirty, HashSet<IWidgetWithLayout>? contentDirty)
    {
        _layoutDirty = layoutDirty;
        _contentDirty = contentDirty;
    }

    /// <summary>All widgets are layout-dirty and content-dirty. Use for first frame or full redraw.</summary>
    public static DirtyMap AllDirty => default;

    public bool IsLayoutDirty(IWidgetWithLayout widget) =>
        _layoutDirty is null || _layoutDirty.Contains(widget);

    public bool IsContentDirty(IWidgetWithLayout widget) =>
        _contentDirty is null || _contentDirty.Contains(widget);
}
