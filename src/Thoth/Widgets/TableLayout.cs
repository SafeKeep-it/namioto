using Thoth.Rendering.Layout;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public class TableLayout : ILayoutCreator
{
    public WidgetSizeRequest Measure(in IWidgetWithLayout widget,
                                     in SizeConstraint constraint,
                                     ReadOnlySpan<WidgetSizeRequest> desires)
    {
        _ = desires;
        var table = widget as Table
            ?? throw new InvalidOperationException($"{nameof(TableLayout)} requires {nameof(Table)}.");

        if (constraint.MaxWidth <= 0 || constraint.MaxHeight <= 0)
            return new(widget, this, new Size(0, 0));

        var columnCount = ResolveColumnCount(table);
        if (columnCount == 0)
            return new(widget, this, new Size(0, 0));

        var widths = ComputeWidths(table, columnCount, constraint.MaxWidth, constraint.MaxHeight);
        var heights = ComputeRowHeights(table, widths, constraint.MaxHeight);
        var measuredHeight = Sum(heights);
        return new(widget, this, new Size(constraint.MaxWidth, Math.Min(constraint.MaxHeight, measuredHeight)));
    }

    public void Arrange(in IWidgetWithLayout widget,
                        in WidgetSize actual,
                        ReadOnlySpan<WidgetSizeRequest> childDesires,
                        Span<WidgetSize> children)
    {
        var table = widget as Table
            ?? throw new InvalidOperationException($"{nameof(TableLayout)} requires {nameof(Table)}.");

        var maxWidth = Math.Max(0, actual.Rect.Width);
        var maxHeight = Math.Max(0, actual.Rect.Height);
        var columnCount = ResolveColumnCount(table);
        var rowCount = table.Rows.Count;
        if (columnCount == 0 || rowCount == 0) return;

        var columnWidths = new int[columnCount];
        var rowHeights = new int[rowCount];

        var childIndex = 0;
        for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            var row = table.Rows[rowIndex];
            for (var columnIndex = 0; columnIndex < row.Cells.Count && childIndex < childDesires.Length; columnIndex++)
            {
                var childSize = childDesires[childIndex].Size;
                columnWidths[columnIndex] = Math.Max(columnWidths[columnIndex], Math.Max(0, childSize.Width));
                rowHeights[rowIndex] = Math.Max(rowHeights[rowIndex], Math.Max(0, childSize.Height));
                childIndex++;
            }
        }

        if (childIndex == 0)
        {
            var baseColumnWidth = columnCount == 0 ? 0 : maxWidth / columnCount;
            var widthRemainder = columnCount == 0 ? 0 : maxWidth % columnCount;
            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
                columnWidths[columnIndex] = baseColumnWidth + (columnIndex < widthRemainder ? 1 : 0);

            for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
                rowHeights[rowIndex] = table.Rows[rowIndex].Cells.Count > 0 ? 1 : 0;
        }
        else
        {
            var totalWidth = Sum(columnWidths);
            var fits = totalWidth <= maxWidth;

            if (!fits)
            {
                var scaledTotal = 0;
                for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    var scaledWidth = totalWidth <= 0
                        ? 0
                        : columnWidths[columnIndex] * maxWidth / totalWidth;
                    columnWidths[columnIndex] = Math.Max(0, scaledWidth);
                    scaledTotal += columnWidths[columnIndex];
                }

                if (columnCount > 0)
                    columnWidths[columnCount - 1] += Math.Max(0, maxWidth - scaledTotal);

                Array.Clear(rowHeights, 0, rowHeights.Length);
                childIndex = 0;
                for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
                {
                    var row = table.Rows[rowIndex];
                    for (var columnIndex = 0; columnIndex < row.Cells.Count && childIndex < children.Length; columnIndex++)
                    {
                        var cell = (IWidgetWithLayout)row.Cells[columnIndex];
                        var cellCreator = cell.GetLayoutCreator();
                        var remeasured = cellCreator.Measure(cell,
                                                             new SizeConstraint(columnWidths[columnIndex], maxHeight),
                                                             ReadOnlySpan<WidgetSizeRequest>.Empty).Size;
                        rowHeights[rowIndex] = Math.Max(rowHeights[rowIndex], Math.Max(0, remeasured.Height));
                        childIndex++;
                    }
                }
            }
        }

        var xOffsets = new int[columnCount];
        var yOffsets = new int[rowCount];
        var x = 0;
        for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            xOffsets[columnIndex] = x;
            x += Math.Max(0, columnWidths[columnIndex]);
        }

        var y = 0;
        for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            yOffsets[rowIndex] = y;
            y += Math.Max(0, rowHeights[rowIndex]);
        }

        childIndex = 0;
        for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            var row = table.Rows[rowIndex];
            for (var columnIndex = 0; columnIndex < row.Cells.Count && childIndex < children.Length; columnIndex++)
            {
                var cell = (IWidgetWithLayout)row.Cells[columnIndex];
                var cellCreator = cell.GetLayoutCreator();

                var clampedX = Math.Clamp(xOffsets[columnIndex], 0, maxWidth);
                var clampedY = Math.Clamp(yOffsets[rowIndex], 0, maxHeight);
                var childWidth = Math.Min(Math.Max(0, columnWidths[columnIndex]), Math.Max(0, maxWidth - clampedX));
                var childHeight = Math.Min(Math.Max(0, rowHeights[rowIndex]), Math.Max(0, maxHeight - clampedY));

                children[childIndex] = new WidgetSize(cell,
                                                      cellCreator,
                                                      new global::Thoth.Rendering.Rect(clampedX, clampedY, childWidth, childHeight));
                childIndex++;
            }
        }
    }

    public void Draw(in IWidgetWithLayout widget, in global::Thoth.Rendering.Canvas canvas)
    {
        _ = widget;
        _ = canvas;
    }

    int ResolveColumnCount(Table table)
    {
        var count = table.Columns.Count;
        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            count = Math.Max(count, table.Rows[rowIndex].Cells.Count);
        return count;
    }

    int[] ComputeWidths(Table table, int columnCount, int totalWidth, int maxHeight)
    {
        var widths = new int[columnCount];
        if (columnCount == 0 || totalWidth <= 0) return widths;

        var flexColumns = new int[columnCount];
        var flexDemandWeights = new int[columnCount];
        var flexCount = 0;
        var fixedWidth = 0;

        for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            var column = columnIndex < table.Columns.Count
                ? table.Columns[columnIndex]
                : new TableColumn(TableColumnWidthMode.Fill);

            if (column.WidthMode == TableColumnWidthMode.Auto)
            {
                var contentWidth = MeasureAutoColumnWidth(table, columnIndex, totalWidth, maxHeight);
                widths[columnIndex] = contentWidth;
                fixedWidth += contentWidth;
                continue;
            }

            var normalizedWeight = Math.Max(1, column.Weight);
            var demandWidth = MeasureAutoColumnWidth(table, columnIndex, totalWidth, maxHeight);
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

        var remaining = Math.Max(0, totalWidth - Sum(widths));
        if (flexCount == 0) return widths;

        var flexibleWidths = ProportionalTableLayout.ComputeColumnWidths(remaining, flexDemandWeights.AsSpan(0, flexCount));
        for (var i = 0; i < flexCount; i++)
            widths[flexColumns[i]] = flexibleWidths[i];

        return widths;
    }

    int MeasureAutoColumnWidth(Table table, int columnIndex, int maxWidth, int maxHeight)
    {
        var measuredWidth = 1;
        var safeHeight = Math.Max(1, maxHeight);
        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            var row = table.Rows[rowIndex];
            if (columnIndex >= row.Cells.Count) continue;
            var cell = (IWidgetWithLayout)row.Cells[columnIndex];
            var cellCreator = cell.GetLayoutCreator();
            var size = cellCreator.Measure(cell,
                                           new SizeConstraint(maxWidth, safeHeight),
                                           Span<WidgetSizeRequest>.Empty).Size;
            measuredWidth = Math.Max(measuredWidth, size.Width);
        }

        return Math.Min(maxWidth, measuredWidth);
    }

    int[] ComputeRowHeights(Table table, int[] widths, int maxHeight)
    {
        var heights = new int[table.Rows.Count];
        if (maxHeight <= 0) return heights;

        var consumed = 0;
        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            var row = table.Rows[rowIndex];
            var rowHeight = 1;
            for (var columnIndex = 0; columnIndex < widths.Length && columnIndex < row.Cells.Count; columnIndex++)
            {
                var width = widths[columnIndex];
                if (width <= 0) continue;
                var remainingHeight = Math.Max(1, maxHeight - consumed);
                var cell = (IWidgetWithLayout)row.Cells[columnIndex];
                var cellCreator = cell.GetLayoutCreator();
                var cellSize = cellCreator.Measure(cell,
                                                   new SizeConstraint(width, remainingHeight),
                                                   Span<WidgetSizeRequest>.Empty).Size;
                rowHeight = Math.Max(rowHeight, Math.Max(1, cellSize.Height));
            }

            if (consumed + rowHeight > maxHeight)
                rowHeight = Math.Max(0, maxHeight - consumed);

            heights[rowIndex] = rowHeight;
            consumed += rowHeight;
            if (consumed >= maxHeight) break;
        }

        return heights;
    }

    int Sum(int[] values)
    {
        var total = 0;
        for (var i = 0; i < values.Length; i++) total += values[i];
        return total;
    }
}
