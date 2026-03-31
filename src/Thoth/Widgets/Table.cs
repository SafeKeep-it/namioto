using Thoth.Rendering;
using Thoth.Rendering.Layout;
using System.Runtime.CompilerServices;

namespace Thoth.Widgets;

public sealed class Table : IWidget, IWidgetWithLayout
{
    readonly TableScribe _scribe;
    readonly List<TableColumn> _columns = [];
    readonly List<TableRow> _rows = [];
    int _layoutVersion;

    public Table()
    {
        _scribe = new(this);
    }

    public IWidget Parent { get; set; } = SentinelWidget.Instance;

    public IReadOnlyList<TableColumn> Columns => _columns;

    public IReadOnlyList<TableRow> Rows => _rows;

    public IWidgetRenderer GetRenderer() => _scribe;

    public IWidgetScribe GetScribe() => _scribe;

    public Thoth.Widgets.Layout.ILayoutCreator GetLayoutCreator() => new TableLayout();

    public TableColumn AddColumn()
    {
        return AddFillColumn();
    }

    public TableColumn AddColumn(int weight)
    {
        return AddProportionalColumn(weight);
    }

    public TableColumn AddAutoColumn()
    {
        var column = new TableColumn(TableColumnWidthMode.Auto);
        _columns.Add(column);
        touch_layout_version();
        return column;
    }

    public TableColumn AddFillColumn(int weight = 1)
    {
        var column = new TableColumn(TableColumnWidthMode.Fill, weight);
        _columns.Add(column);
        touch_layout_version();
        return column;
    }

    public TableColumn AddProportionalColumn(int weight = 1)
    {
        var column = new TableColumn(TableColumnWidthMode.Proportional, weight);
        _columns.Add(column);
        touch_layout_version();
        return column;
    }

    public TableRow AddRow(params IWidget[] cells)
    {
        var row = new TableRow(cells);
        _rows.Add(row);
        for (var i = 0; i < row.Cells.Count; i++)
            row.Cells[i].Parent = this;

        touch_layout_version();

        return row;
    }

    public void ClearRows()
    {
        RenderPhaseGuard.ThrowIfActive("Table.ClearRows");
        if (_rows.Count > 0)
            touch_layout_version();
        _rows.Clear();
    }

    internal int LayoutVersion => _layoutVersion;

    void touch_layout_version()
    {
        unchecked
        {
            _layoutVersion++;
        }
    }

    public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
    {
        for (var rowIndex = 0; rowIndex < _rows.Count; rowIndex++)
        {
            var row = _rows[rowIndex];
            for (var cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
            {
                if (!visitor.Visit(row.Cells[cellIndex])) return;
            }
        }
    }

    public void Accept<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IVisitor, allows ref struct
    {
        for (var rowIndex = 0; rowIndex < _rows.Count; rowIndex++)
        {
            var row = _rows[rowIndex];
            for (var cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
                visitor.Visit((IWidgetWithLayout)row.Cells[cellIndex]);
        }
    }

}
public sealed record TableColumn
{
    TableColumnWidthMode _widthMode;
    int _weight;

    public TableColumn(TableColumnWidthMode widthMode = TableColumnWidthMode.Fill, int weight = 1)
    {
        WidthMode = widthMode;
        Weight = weight;
    }

    public TableColumnWidthMode WidthMode
    {
        get => _widthMode;
        set => _widthMode = value;
    }

    public int Weight
    {
        get => _weight;
        set => _weight = Math.Max(1, value);
    }
}

public sealed class TableRow
{
    readonly List<IWidget> _cells = [];

    public TableRow(params IWidget[] cells)
    {
        _cells.AddRange(cells);
    }

    public IReadOnlyList<IWidget> Cells => _cells;
}
