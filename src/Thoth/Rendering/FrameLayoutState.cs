namespace Thoth.Rendering;

using Thoth.Navigation.Focus;
using Thoth.Widgets;

public sealed class FrameLayoutState
{
    struct LayoutEntry
    {
        public readonly Rect Rect;
        public readonly int DrawOrder;

        public LayoutEntry(Rect rect, int drawOrder)
        {
            Rect = rect;
            DrawOrder = drawOrder;
        }
    }

    readonly struct HitEntry
    {
        public readonly IWidget Widget;
        public readonly Rect Rect;

        public HitEntry(IWidget widget, Rect rect)
        {
            Widget = widget;
            Rect = rect;
        }
    }

    public readonly record struct FocusableLayoutItem(IWidget Widget, Rect Rect, int DrawOrder);

    readonly Dictionary<IWidget, LayoutEntry> _layouts = new(ReferenceEqualityComparer.Instance);
    readonly List<HitEntry> _entriesByDrawOrder = [];
    readonly Dictionary<int, List<HitEntry>> _rowIndex = [];
    readonly List<FocusableLayoutItem> _focusables = [];
    bool _hitIndexDirty = true;
    long _layoutVersion;
    int _nextAutoDrawOrder;

    public long LayoutVersion => _layoutVersion;

    public void BeginLayout()
    {
        _layoutVersion++;
        _layouts.Clear();
        _entriesByDrawOrder.Clear();
        _rowIndex.Clear();
        _focusables.Clear();
        _hitIndexDirty = true;
        _nextAutoDrawOrder = 0;
    }

    public void Set(IWidget widget, Rect rect)
    {
        Set(widget, rect, _nextAutoDrawOrder++);
    }

    public void Set(IWidget widget, Rect rect, int drawOrder)
    {
        _layouts[widget] = new(rect, drawOrder);
        if (drawOrder >= _nextAutoDrawOrder)
            _nextAutoDrawOrder = drawOrder + 1;
        _hitIndexDirty = true;
    }

    public bool TryGetRect(IWidget widget, out Rect rect)
    {
        if (_layouts.TryGetValue(widget, out var entry))
        {
            rect = entry.Rect;
            return true;
        }

        rect = new(0, 0, 0, 0);
        return false;
    }

    public int DrawOrderOf(IWidget widget)
    {
        return _layouts.TryGetValue(widget, out var entry) ? entry.DrawOrder : int.MinValue;
    }

    public void CollectDetachedRects(IWidget root, List<Rect> detachedRects)
    {
        detachedRects.Clear();

        var liveWidgets = new HashSet<IWidget>(ReferenceEqualityComparer.Instance) { root };
        var stack = new Stack<IWidget>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var widget = stack.Pop();
            var visitor = new PushToStackVisitor(stack);
            WidgetTraversalExtensions.VisitChildrenReverse(widget, ref visitor);

            foreach (var child in stack)
                liveWidgets.Add(child);
        }

        foreach (var pair in _layouts)
        {
            if (liveWidgets.Contains(pair.Key)) continue;
            detachedRects.Add(pair.Value.Rect);
        }

        if (detachedRects.Count == 0) return;

        foreach (var pair in _layouts.Keys.ToArray())
            if (!liveWidgets.Contains(pair))
                _layouts.Remove(pair);

        _hitIndexDirty = true;
    }

    public IWidget WidgetAt(int x, int y)
    {
        EnsureHitIndex();

        if (!_rowIndex.TryGetValue(y, out var row))
        {
            row = BuildRow(y);
            _rowIndex[y] = row;
        }

        for (var i = 0; i < row.Count; i++)
        {
            var entry = row[i];
            if (x >= entry.Rect.X && x < entry.Rect.X + entry.Rect.Width)
                return entry.Widget;
        }

        return SentinelWidget.Instance;
    }

    public IReadOnlyList<FocusableLayoutItem> FocusableItems()
    {
        EnsureHitIndex();
        return _focusables;
    }

    void EnsureHitIndex()
    {
        if (!_hitIndexDirty) return;

        _entriesByDrawOrder.Clear();
        _focusables.Clear();
        _rowIndex.Clear();

        foreach (var pair in _layouts)
        {
            var widget = pair.Key;
            var entry = pair.Value;

            _entriesByDrawOrder.Add(new(widget, entry.Rect));
            if (widget is IFocusable)
                _focusables.Add(new(widget, entry.Rect, entry.DrawOrder));
        }

        _entriesByDrawOrder.Sort((left, right) => DrawOrderCompare(left, right));
        _focusables.Sort((left, right) => right.DrawOrder.CompareTo(left.DrawOrder));
        _hitIndexDirty = false;
    }

    List<HitEntry> BuildRow(int y)
    {
        var row = new List<HitEntry>();
        for (var i = 0; i < _entriesByDrawOrder.Count; i++)
        {
            var entry = _entriesByDrawOrder[i];
            if (y < entry.Rect.Y || y >= entry.Rect.Y + entry.Rect.Height) continue;
            row.Add(entry);
        }

        return row;
    }

    int DrawOrderCompare(HitEntry left, HitEntry right)
    {
        var leftOrder = DrawOrderOf(left.Widget);
        var rightOrder = DrawOrderOf(right.Widget);
        return rightOrder.CompareTo(leftOrder);
    }
}
