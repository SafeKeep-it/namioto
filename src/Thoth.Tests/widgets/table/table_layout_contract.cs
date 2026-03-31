using Shouldly;
using Thoth.Rendering;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Thoth.Tests.widgets.table;

public class table_layout_contract
{
    [Fact]
    public void columns_share_available_width_equally_when_weights_match()
    {
        var table = new Table();
        table.AddColumn(1);
        table.AddColumn(1);
        table.AddColumn(1);

        var c1 = new probe_cell();
        var c2 = new probe_cell();
        var c3 = new probe_cell();
        table.AddRow(c1, c2, c3);

        var root = new Screen();
        root.Add(table);
        var buffer = new ScreenBuffer(30, 8);
        var layout = tree_render_harness.Render(root, buffer);
        WriteSnapshots(buffer, table, 30, 8, "table_layout_contract.equal_columns.svg", layout);

        layout.TryGetRect(c1, out var r1).ShouldBeTrue();
        layout.TryGetRect(c2, out var r2).ShouldBeTrue();
        layout.TryGetRect(c3, out var r3).ShouldBeTrue();

        r1.Width.ShouldBe(10);
        r2.Width.ShouldBe(10);
        r3.Width.ShouldBe(10);
        r1.X.ShouldBe(0);
        r2.X.ShouldBe(10);
        r3.X.ShouldBe(20);
    }

    [Fact]
    public void rows_adapt_to_maximum_measured_height_of_cells()
    {
        var table = new Table();
        table.AddColumn(1);
        table.AddColumn(1);
        table.AddColumn(1);

        var row0a = new probe_cell { PreferredHeight = 1 };
        var row0b = new probe_cell { PreferredHeight = 3 };
        var row0c = new probe_cell { PreferredHeight = 2 };
        var row1a = new probe_cell { PreferredHeight = 1 };
        var row1b = new probe_cell { PreferredHeight = 1 };
        var row1c = new probe_cell { PreferredHeight = 1 };

        table.AddRow(row0a, row0b, row0c);
        table.AddRow(row1a, row1b, row1c);

        var root = new Screen();
        root.Add(table);
        var buffer = new ScreenBuffer(30, 8);
        var layout = tree_render_harness.Render(root, buffer);
        WriteSnapshots(buffer, table, 30, 8, "table_layout_contract.row_heights.svg", layout);

        layout.TryGetRect(row0a, out var row0aRect).ShouldBeTrue();
        layout.TryGetRect(row0b, out var row0bRect).ShouldBeTrue();
        layout.TryGetRect(row1a, out var row1aRect).ShouldBeTrue();

        row0aRect.Height.ShouldBe(3);
        row0bRect.Height.ShouldBe(3);
        row1aRect.Y.ShouldBe(3);
        row1aRect.Height.ShouldBe(1);
    }

    [Fact]
    public void width_distribution_rounding_keeps_total_width_exact()
    {
        var table = new Table();
        table.AddColumn(2);
        table.AddColumn(1);
        table.AddColumn(1);

        var c1 = new probe_cell();
        var c2 = new probe_cell();
        var c3 = new probe_cell();
        table.AddRow(c1, c2, c3);

        var root = new Screen();
        root.Add(table);
        var buffer = new ScreenBuffer(31, 4);
        var layout = tree_render_harness.Render(root, buffer);
        WriteSnapshots(buffer, table, 31, 4, "table_layout_contract.total_width_exact.svg", layout);

        layout.TryGetRect(c1, out var r1).ShouldBeTrue();
        layout.TryGetRect(c2, out var r2).ShouldBeTrue();
        layout.TryGetRect(c3, out var r3).ShouldBeTrue();

        (r1.Width + r2.Width + r3.Width).ShouldBe(31);
    }

    [Fact]
    public void auto_and_fill_columns_align_marker_and_expand_label_column()
    {
        var table = new Table();
        table.AddAutoColumn();
        table.AddFillColumn();

        var marker = new fixed_width_cell { PreferredWidth = 1 };
        var label = new fixed_width_cell { PreferredWidth = 3 };
        table.AddRow(marker, label);

        var root = new Screen();
        root.Add(table);
        var buffer = new ScreenBuffer(20, 4);
        var layout = tree_render_harness.Render(root, buffer);
        WriteSnapshots(buffer, table, 20, 4, "table_layout_contract.auto_fill.svg", layout);

        layout.TryGetRect(marker, out var markerRect).ShouldBeTrue();
        layout.TryGetRect(label, out var labelRect).ShouldBeTrue();

        markerRect.Width.ShouldBe(1);
        labelRect.X.ShouldBe(1);
        labelRect.Width.ShouldBe(19);
    }

    [Fact]
    public void flexible_columns_bias_width_toward_larger_content_demand()
    {
        var table = new Table();
        table.AddFillColumn();
        table.AddFillColumn();

        var shortCell = new fixed_width_cell { PreferredWidth = 3 };
        var longCell = new fixed_width_cell { PreferredWidth = 12 };
        table.AddRow(shortCell, longCell);

        var root = new Screen();
        root.Add(table);
        var buffer = new ScreenBuffer(20, 3);
        var layout = tree_render_harness.Render(root, buffer);
        WriteSnapshots(buffer, table, 20, 3, "table_layout_contract.flex_demand_bias.svg", layout);

        layout.TryGetRect(shortCell, out var shortRect).ShouldBeTrue();
        layout.TryGetRect(longCell, out var longRect).ShouldBeTrue();

        longRect.Width.ShouldBeGreaterThan(shortRect.Width);
        (shortRect.Width + longRect.Width).ShouldBe(20);
    }

    static void WriteSnapshots(ScreenBuffer buffer,
                               IWidget root,
                               int width,
                               int height,
                               string name,
                               FrameLayoutState layout)
    {
        buffer.WriteTerminalSnapshotSvg(name);
        buffer.WriteLayoutDebugSvg(root, width, height, name, layoutState: layout);
    }

    sealed class probe_cell : TestWidgetBase
    {
        public int PreferredHeight { get; set; } = 1;

        public override Size Measure(SizeConstraint constraint)
        {
            return new(constraint.MaxWidth, Math.Min(constraint.MaxHeight, PreferredHeight));
        }

        public override void Render(Canvas canvas)
        {
            _ = canvas;
        }
    }

    sealed class fixed_width_cell : TestWidgetBase
    {
        public int PreferredWidth { get; set; } = 1;

        public override Size Measure(SizeConstraint constraint)
        {
            return new(Math.Min(constraint.MaxWidth, PreferredWidth), Math.Min(constraint.MaxHeight, 1));
        }

        public override void Render(Canvas canvas)
        {
            _ = canvas;
        }
    }
}
