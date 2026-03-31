namespace Thoth.Rendering.Grid;

public sealed class GridBuffer
{
    const int ShrinkThresholdDivisor = 4;

    Cell[] _cells;

    public GridBuffer(int width, int height)
    {
        Width = width;
        Height = height;
        _cells = new Cell[width * height];
    }

    public int Width { get; private set; }
    public int Height { get; private set; }

    public void Resize(int width, int height)
    {
        var required = width * height;

        if (required > _cells.Length || required <= _cells.Length / ShrinkThresholdDivisor)
            _cells = new Cell[required];

        Width = width;
        Height = height;
    }

    public void SetCell(int x, int y, Cell cell, ushort frameNumber = 0)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        _cells[y * Width + x] = new(cell.GlyphId, cell.StyleIndex, cell.Width, frameNumber);
    }

    public void SetCellUnchecked(int x, int y, Cell cell)
    {
        _cells[y * Width + x] = cell;
    }

    public Cell GetCell(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return default;
        return _cells[y * Width + x];
    }

    public void FillRect(int x, int y, int width, int height, Cell cell, ushort frameNumber = 0)
    {
        if (width <= 0 || height <= 0) return;

        var startX = Math.Max(0, x);
        var startY = Math.Max(0, y);
        var endX = Math.Min(Width, x + width);
        var endY = Math.Min(Height, y + height);
        if (startX >= endX || startY >= endY) return;

        var rowWidth = endX - startX;
        var rowCount = endY - startY;
        var value = new Cell(cell.GlyphId, cell.StyleIndex, cell.Width, frameNumber);

        if (rowWidth == Width)
        {
            _cells.AsSpan(startY * Width, rowCount * Width).Fill(value);
            return;
        }

        if (rowWidth == 1)
        {
            for (var row = 0; row < rowCount; row++)
                _cells[(startY + row) * Width + startX] = value;
            return;
        }

        var firstRow = _cells.AsSpan(startY * Width + startX, rowWidth);
        firstRow.Fill(value);

        for (var row = 1; row < rowCount; row++)
        {
            var targetRow = _cells.AsSpan((startY + row) * Width + startX, rowWidth);
            firstRow.CopyTo(targetRow);
        }
    }

    public void ClearRect(int x, int y, int width, int height, int styleIndex = 0, ushort frameNumber = 0)
    {
        FillRect(x, y, width, height, new Cell(Cell.Empty.GlyphId, styleIndex, Cell.Empty.Width), frameNumber);
    }

    public void WriteAsciiRunUnchecked(int x,
                                       int y,
                                       ReadOnlySpan<byte> ascii,
                                       int styleIndex,
                                       ushort frameNumber = 0)
    {
        var row = _cells.AsSpan(y * Width + x, ascii.Length);
        for (var i = 0; i < ascii.Length; i++)
            row[i] = new(ascii[i], styleIndex, 1, frameNumber);
    }
}
