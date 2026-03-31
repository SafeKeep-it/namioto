using System.Text;
using Shouldly;
using Thoth.Rendering;
using Thoth.Terminal.Raw.Egress;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.border_rendering;

public class border : IAsyncLifetime
{
    ScreenBuffer _single = null!;
    ScreenBuffer _rounded = null!;
    ScreenBuffer _outline = null!;
    ScreenBuffer _inset = null!;
    ScreenBuffer _labeled = null!;
    FillWidget _fill = null!;

    public Task InitializeAsync()
    {
        _fill = new FillWidget { Character = '.' };
        _single = render(BorderStyle.Single, "border_rendering.single_border.svg");
        _rounded = render(BorderStyle.Rounded, "border_rendering.rounded_border.svg");
        _outline = render(BorderStyle.Outline, "border_rendering.outline_border.svg");
        _inset = render(BorderStyle.Inset, "border_rendering.inset_border.svg");
        _labeled = render_labeled("border_rendering.labels.svg");

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void draws_border_and_content()
    {
        var rows = get_rows(_single, 10, 5);

        // Expected:
        // 0: ┌────────┐
        // 1: │........│
        // 2: │........│
        // 3: │........│
        // 4: └────────┘

        rows[0].ShouldBe("┌────────┐");
        rows[1].ShouldBe("│........│");
        rows[2].ShouldBe("│........│");
        rows[3].ShouldBe("│........│");
        rows[4].ShouldBe("└────────┘");
    }

    [Fact]
    public void draws_rounded_border_and_content()
    {
        var rows = get_rows(_rounded, 10, 5);

        rows[0].ShouldBe("╭────────╮");
        rows[1].ShouldBe("│........│");
        rows[2].ShouldBe("│........│");
        rows[3].ShouldBe("│........│");
        rows[4].ShouldBe("╰────────╯");
    }

    [Fact]
    public void draws_outline_border_and_content()
    {
        var rows = get_rows(_outline, 10, 5);

        rows[0].ShouldBe("▛▔▔▔▔▔▔▔▔▜");
        rows[1].ShouldBe("▏........▕");
        rows[2].ShouldBe("▏........▕");
        rows[3].ShouldBe("▏........▕");
        rows[4].ShouldBe("▙▁▁▁▁▁▁▁▁▟");
    }

    [Fact]
    public void draws_inset_border_and_content()
    {
        var rows = get_rows(_inset, 10, 5);

        rows[0].ShouldBe("╔════════╗");
        rows[1].ShouldBe("║........║");
        rows[2].ShouldBe("║........║");
        rows[3].ShouldBe("║........║");
        rows[4].ShouldBe("╚════════╝");
    }

    [Fact]
    public void draws_top_center_and_bottom_left_labels_on_border()
    {
        var rows = get_rows(_labeled, 14, 5);

        rows[0].ShouldBe("┌───title────┐");
        rows[4].ShouldBe("└C copy──────┘");
    }

    [Fact]
    public void throws_when_inset_border_uses_labels()
    {
        var border = new Border
                     {
                         BorderStyle = BorderStyle.Inset,
                         Content = _fill
                     };
        border.Labels.TopCenter = "title";

        var buffer = new ScreenBuffer(10, 5);

        Should.Throw<InvalidOperationException>(() => tree_render_harness.Render(border, buffer));
    }

    [Fact]
    public void when_border_measures_fixed_content_then_it_adds_one_cell_per_side()
    {
        var border = new Border { Content = new fixed_size_widget(4, 2) };

        var measured = border.GetRenderer().Measure(new(80, 25));

        measured.Width.ShouldBe(6);
        measured.Height.ShouldBe(4);
    }

    [Fact]
    public void when_border_is_arranged_then_content_receives_inner_rect_minus_two()
    {
        var fill = new FillWidget { Character = '.' };
        var border = new Border { Content = fill };
        var engine = new FrameRenderer(fullRender: false);
        engine.RenderFrame(border, new UiContext(border), 10, 5, new Dictionary<IWidget, InvalidationKind>());

        engine.LayoutState.TryGetRect(fill, out var contentRect).ShouldBeTrue();
        contentRect.X.ShouldBe(1);
        contentRect.Y.ShouldBe(1);
        contentRect.Width.ShouldBe(8);
        contentRect.Height.ShouldBe(3);
    }

    [Fact]
    public void when_outline_border_has_background_color_then_border_and_inside_cells_use_background()
    {
        var background = new Color(32, 64, 96);
        var border = new Border
                     {
                         BorderStyle = BorderStyle.Outline,
                         BorderColor = Color.White,
                         BackgroundColor = background,
                         Content = new fixed_size_widget(4, 2)
                     };

        var (buffer, context) = render_for_style(border, 6, 4);

        var cornerStyle = context.Styles.Get(buffer.GetCell(0, 0).StyleIndex);
        var innerStyle = context.Styles.Get(buffer.GetCell(2, 2).StyleIndex);

        cornerStyle.Background.ShouldBe(background);
        innerStyle.Background.ShouldBe(background);
    }

    [Fact]
    public void when_inset_border_has_background_color_then_only_inside_cells_use_background()
    {
        var background = new Color(20, 40, 80);
        var border = new Border
                     {
                         BorderStyle = BorderStyle.Inset,
                         BorderColor = Color.White,
                         BackgroundColor = background,
                         Content = new fixed_size_widget(4, 2)
                     };

        var (buffer, context) = render_for_style(border, 6, 4);

        var cornerStyle = context.Styles.Get(buffer.GetCell(0, 0).StyleIndex);
        var innerStyle = context.Styles.Get(buffer.GetCell(2, 2).StyleIndex);

        cornerStyle.Background.ShouldBeNull();
        innerStyle.Background.ShouldBe(background);
    }

    ScreenBuffer render(BorderStyle style, string snapshot)
    {
        var buffer = new ScreenBuffer(10, 5);
        var border = new Border { Content = _fill, BorderStyle = style };
        tree_render_harness.Render(border, buffer);

        buffer.WriteTerminalSnapshotSvg(snapshot);
        buffer.WriteLayoutDebugSvg(border, 10, 5, snapshot);
        return buffer;
    }

    static (ScreenBuffer buffer, RenderContext context) render_for_style(Border border, int width, int height)
    {
        var buffer = new ScreenBuffer(width, height);
        var uiContext = new UiContext(new Screen());
        tree_render_harness.Render(border, buffer, uiContext);
        var context = new RenderContext(uiContext);

        return (buffer, context);
    }

    ScreenBuffer render_labeled(string snapshot)
    {
        var buffer = new ScreenBuffer(14, 5);
        var border = new Border { Content = _fill };
        border.Labels.TopCenter = "title";
        border.Labels.BottomLeft = "C copy";
        tree_render_harness.Render(border, buffer);

        buffer.WriteTerminalSnapshotSvg(snapshot);
        buffer.WriteLayoutDebugSvg(border, 14, 5, snapshot);
        return buffer;
    }

    static string[] get_rows(ScreenBuffer buffer, int width, int height)
    {
        var result = new string[height];
        for (var y = 0; y < height; y++)
        {
            var sb = new StringBuilder(width);
            for (var x = 0; x < width; x++)
            {
                var cell = buffer.GetCell(x, y);
                sb.Append(cell.GlyphId == 0 ? ' ' : (char)cell.GlyphId);
            }

            result[y] = sb.ToString();
        }

        return result;
    }
}

public class FillWidget : TestWidgetBase
{
    public char Character { get; set; } = ' ';

    public override void Render(Canvas canvas)
    {
        canvas.Fill(0, 0, canvas.Width, canvas.Height, (Rune)Character, new());
    }

    public override Size Measure(SizeConstraint constraint) =>
        new(constraint.MaxWidth, constraint.MaxHeight);
}

sealed class fixed_size_widget(int width, int height) : TestWidgetBase
{
    public override Size Measure(SizeConstraint constraint) => new(width, height);

    public override void Render(Canvas canvas)
    {
    }
}
