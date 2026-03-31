using System.Runtime.CompilerServices;
using Thoth.Rendering;
using Thoth.Rendering.Layout;

namespace Thoth.Widgets;

public sealed class TableScribe : IWidgetRenderer, IWidgetScribe
{
    readonly Table _widget;
    int[] _columnWidths = [];
    int[] _rowHeights = [];
    List<column_width_cache_entry>? _columnWidthCache;
    List<row_height_cache_entry>? _rowHeightCache;
    readonly List<Canvas.ChildPlacement> _childPlacements = [];

    public TableScribe(Table widget)
    {
        _widget = widget;
    }

    public Size Measure(SizeConstraint constraint)
    {
        if (constraint.MaxWidth <= 0 || constraint.MaxHeight <= 0)
        {
            _columnWidths = [];
            _rowHeights = [];
            return new(0, 0);
        }

        var columnCount = resolve_column_count();
        if (columnCount == 0)
        {
            _columnWidths = [];
            _rowHeights = [];
            return new(0, 0);
        }

        var widths = compute_widths(columnCount, constraint.MaxWidth, constraint.MaxHeight);
        var heights = compute_row_heights(widths, constraint.MaxHeight);
        var measuredHeight = 0;
        for (var i = 0; i < heights.Length; i++)
            measuredHeight += heights[i];

        _columnWidths = widths;
        _rowHeights = heights;
        return new(constraint.MaxWidth, Math.Min(constraint.MaxHeight, measuredHeight));
    }

    public void Arrange(Rect rect)
    {
        _childPlacements.Clear();

        if (rect.Width <= 0 || rect.Height <= 0) return;

        var columnCount = resolve_column_count();
        if (columnCount == 0) return;

        var widths = compute_widths(columnCount, rect.Width, rect.Height);
        var heights = compute_row_heights(widths, rect.Height);

        var y = 0;
        for (var rowIndex = 0; rowIndex < _widget.Rows.Count && y < rect.Height; rowIndex++)
        {
            var row = _widget.Rows[rowIndex];
            var rowHeight = heights[rowIndex];
            if (rowHeight <= 0) continue;

            var x = 0;
            for (var columnIndex = 0; columnIndex < widths.Length && x < rect.Width; columnIndex++)
            {
                var width = widths[columnIndex];
                if (width <= 0) continue;
                if (columnIndex >= row.Cells.Count)
                {
                    x += width;
                    continue;
                }

                var childRect = new Rect(x, y, width, rowHeight);
                row.Cells[columnIndex].GetRenderer().Arrange(childRect);
                _childPlacements.Add(new(row.Cells[columnIndex], childRect));
                x += width;
            }

            y += rowHeight;
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

    int resolve_column_count()
    {
        var count = _widget.Columns.Count;
        for (var rowIndex = 0; rowIndex < _widget.Rows.Count; rowIndex++)
            count = Math.Max(count, _widget.Rows[rowIndex].Cells.Count);
        return count;
    }

    int[] compute_widths(int columnCount, int totalWidth, int maxHeight)
    {
        if (try_get_cached_widths(columnCount, totalWidth, maxHeight, out var cachedWidths))
            return cachedWidths;

        var widths = new int[columnCount];
        if (columnCount == 0 || totalWidth <= 0) return widths;

        var fixedWidth = 0;
        var flexCount = 0;

        Span<int> flexStack1 = stackalloc int[64];
        using var flexColumnsBuf = StackBuffer<int>.Create(flexStack1, columnCount);
        var flexColumns = flexColumnsBuf.Span;

        Span<int> flexStack2 = stackalloc int[64];
        using var flexDemandWeightsBuf = StackBuffer<int>.Create(flexStack2, columnCount);
        var flexDemandWeights = flexDemandWeightsBuf.Span;

        for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            var column = columnIndex < _widget.Columns.Count
                ? _widget.Columns[columnIndex]
                : new TableColumn(TableColumnWidthMode.Fill);

            if (column.WidthMode == TableColumnWidthMode.Auto)
            {
                var contentWidth = measure_auto_column_width(columnIndex, totalWidth, maxHeight);
                widths[columnIndex] = contentWidth;
                fixedWidth += contentWidth;
                continue;
            }

            var normalizedWeight = Math.Max(1, column.Weight);
            var demandWidth = measure_auto_column_width(columnIndex, totalWidth, maxHeight);
            flexColumns[flexCount] = columnIndex;
            flexDemandWeights[flexCount] = Math.Max(1, demandWidth * normalizedWeight);
            flexCount++;
        }

        if (fixedWidth > totalWidth)
        {
            var overflow = fixedWidth - totalWidth;
            for (var i = widths.Length - 1; i >= 0 && overflow > 0; i--)
            {
                if (widths[i] <= 0) continue;
                var take = Math.Min(widths[i], overflow);
                widths[i] -= take;
                overflow -= take;
            }
        }

        var remaining = Math.Max(0, totalWidth - sum(widths));
        if (flexCount == 0)
        {
            set_cached_widths(columnCount, totalWidth, maxHeight, widths);
            return widths;
        }

        var flexibleWidths = ProportionalTableLayout.ComputeColumnWidths(remaining, flexDemandWeights[..flexCount]);
        for (var i = 0; i < flexCount; i++)
            widths[flexColumns[i]] = flexibleWidths[i];

        set_cached_widths(columnCount, totalWidth, maxHeight, widths);
        return widths;
    }

    bool try_get_cached_widths(int columnCount, int totalWidth, int maxHeight, out int[] widths)
    {
        widths = [];
        if (_columnWidthCache is null || _columnWidthCache.Count == 0) return false;

        var contentSignature = compute_content_signature();
        for (var i = 0; i < _columnWidthCache.Count; i++)
        {
            var entry = _columnWidthCache[i];
            if (entry.LayoutVersion != _widget.LayoutVersion) continue;
            if (entry.TotalWidth != totalWidth) continue;
            if (entry.MaxHeight != maxHeight) continue;
            if (entry.ColumnCount != columnCount) continue;
            if (entry.ContentSignature != contentSignature) continue;
            widths = entry.ColumnWidths;
            return true;
        }

        return false;
    }

    void set_cached_widths(int columnCount, int totalWidth, int maxHeight, int[] widths)
    {
        _columnWidthCache ??= new List<column_width_cache_entry>(4);
        var entry = new column_width_cache_entry(_widget.LayoutVersion, totalWidth, maxHeight, columnCount, compute_content_signature(), widths);
        if (_columnWidthCache.Count >= 4) _columnWidthCache.RemoveAt(0);
        _columnWidthCache.Add(entry);
    }

    int measure_auto_column_width(int columnIndex, int maxWidth, int maxHeight)
    {
        var measuredWidth = 1;
        var safeHeight = Math.Max(1, maxHeight);
        for (var rowIndex = 0; rowIndex < _widget.Rows.Count; rowIndex++)
        {
            var row = _widget.Rows[rowIndex];
            if (columnIndex >= row.Cells.Count) continue;
            var cell = row.Cells[columnIndex];
            var size = cell.GetRenderer().Measure(new(maxWidth, safeHeight));
            measuredWidth = Math.Max(measuredWidth, size.Width);
        }

        return Math.Min(maxWidth, measuredWidth);
    }

    int[] compute_row_heights(int[] widths, int maxHeight)
    {
        if (try_get_cached_row_heights(widths, maxHeight, out var cachedHeights)) return cachedHeights;

        var heights = new int[_widget.Rows.Count];
        var consumed = 0;
        for (var rowIndex = 0; rowIndex < _widget.Rows.Count; rowIndex++)
        {
            var row = _widget.Rows[rowIndex];
            var rowHeight = 1;
            for (var colIndex = 0; colIndex < widths.Length && colIndex < row.Cells.Count; colIndex++)
            {
                var width = widths[colIndex];
                if (width <= 0) continue;
                var remainingHeight = Math.Max(1, maxHeight - consumed);
                var cellSize = row.Cells[colIndex].GetRenderer().Measure(new(width, remainingHeight));
                rowHeight = Math.Max(rowHeight, Math.Max(1, cellSize.Height));
            }

            if (consumed + rowHeight > maxHeight) rowHeight = Math.Max(0, maxHeight - consumed);
            heights[rowIndex] = rowHeight;
            consumed += rowHeight;
            if (consumed >= maxHeight) break;
        }

        set_cached_row_heights(widths, maxHeight, heights);
        return heights;
    }

    bool try_get_cached_row_heights(int[] widths, int maxHeight, out int[] heights)
    {
        heights = [];
        if (_rowHeightCache is null || _rowHeightCache.Count == 0) return false;

        var widthsHash = compute_widths_hash(widths);
        var contentSignature = compute_content_signature();
        for (var i = 0; i < _rowHeightCache.Count; i++)
        {
            var entry = _rowHeightCache[i];
            if (entry.LayoutVersion != _widget.LayoutVersion) continue;
            if (entry.MaxHeight != maxHeight) continue;
            if (entry.WidthsHash != widthsHash) continue;
            if (entry.ContentSignature != contentSignature) continue;
            if (entry.RowCount != _widget.Rows.Count) continue;
            if (entry.ColumnCount != widths.Length) continue;
            heights = entry.RowHeights;
            return true;
        }

        return false;
    }

    void set_cached_row_heights(int[] widths, int maxHeight, int[] heights)
    {
        _rowHeightCache ??= new List<row_height_cache_entry>(4);
        var entry = new row_height_cache_entry(_widget.LayoutVersion, maxHeight, widths.Length, _widget.Rows.Count, compute_widths_hash(widths), compute_content_signature(), heights);
        if (_rowHeightCache.Count >= 4) _rowHeightCache.RemoveAt(0);
        _rowHeightCache.Add(entry);
    }

    static int compute_widths_hash(int[] widths)
    {
        var hash = new HashCode();
        hash.Add(widths.Length);
        for (var i = 0; i < widths.Length; i++) hash.Add(widths[i]);
        return hash.ToHashCode();
    }

    int compute_content_signature()
    {
        var hash = new HashCode();
        hash.Add(_widget.LayoutVersion);
        hash.Add(_widget.Rows.Count);
        for (var rowIndex = 0; rowIndex < _widget.Rows.Count; rowIndex++)
        {
            var row = _widget.Rows[rowIndex];
            hash.Add(row.Cells.Count);
            for (var cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
            {
                var cell = row.Cells[cellIndex];
                hash.Add(RuntimeHelpers.GetHashCode(cell));
                if (cell is TextBlock textBlock)
                    hash.Add(textBlock.ContentVersion);
            }
        }

        return hash.ToHashCode();
    }

    static int sum(int[] values)
    {
        var total = 0;
        for (var i = 0; i < values.Length; i++) total += values[i];
        return total;
    }

    readonly record struct column_width_cache_entry(int LayoutVersion,
                                                    int TotalWidth,
                                                    int MaxHeight,
                                                    int ColumnCount,
                                                    int ContentSignature,
                                                    int[] ColumnWidths);

    readonly record struct row_height_cache_entry(int LayoutVersion,
                                                  int MaxHeight,
                                                  int ColumnCount,
                                                  int RowCount,
                                                  int WidthsHash,
                                                  int ContentSignature,
                                                  int[] RowHeights);
}
