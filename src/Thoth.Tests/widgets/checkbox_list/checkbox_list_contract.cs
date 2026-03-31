using Shouldly;
using Comptatata.Tests.App.Cli;
using Thoth;
using Thoth.Eventing;
using Thoth.Modal;
using Thoth.Rendering;
using Thoth.Rendering.Grid;
using Thoth.Widgets;

namespace Thoth.Tests.widgets.checkbox_list;

public class checkbox_list_contract
{
    static readonly Color default_checked_color = new(46, 160, 67);

    [Fact]
    public void up_down_and_space_toggle_active_row_while_tab_is_ignored()
    {
        var terminal = new MockTerminal();
        var list = new MultipleChoiceList();
        list.SetChoices([
            new("choice-a", "Alpha"),
            new("choice-b", "Beta")
        ]);

        var root = new Screen();
        root.Add(list);

        var attention = new AttentionManager(terminal, root, keyboardFocus: list);
        attention.Render();

        attention.HandleKey(new(' ', ConsoleKey.Spacebar, false, false, false));
        attention.HandleKey(new('\0', ConsoleKey.DownArrow, false, false, false));
        attention.HandleKey(new(' ', ConsoleKey.Spacebar, false, false, false));
        attention.HandleKey(new('\t', ConsoleKey.Tab, false, false, false));
        attention.HandleKey(new('\0', ConsoleKey.UpArrow, false, false, false));
        attention.HandleKey(new(' ', ConsoleKey.Spacebar, false, false, false));

        list.SelectedChoiceIds.Count.ShouldBe(1);
        list.SelectedChoiceIds[0].ShouldBe("choice-b");
    }

    [Fact]
    public void text_column_starts_one_space_after_checkbox_column()
    {
        var list = new MultipleChoiceList();
        list.SetChoices([
            new("choice-a", "Alpha"),
            new("choice-b", "Beta")
        ]);

        var root = new Screen();
        root.Add(list);

        var frame = Render(root, 24, 4);

        frame.GlyphAt(0, 0).ShouldBe('☐');
        frame.GlyphAt(1, 0).ShouldBe(' ');
        frame.GlyphAt(2, 0).ShouldBe('A');

        frame.GlyphAt(0, 1).ShouldBe('☐');
        frame.GlyphAt(1, 1).ShouldBe(' ');
        frame.GlyphAt(2, 1).ShouldBe('B');
    }

    [Fact]
    public void active_row_background_applies_to_marker_gap_and_text_cells()
    {
        var active = new Color(10, 20, 30);
        var list = new MultipleChoiceList
                   {
                       ActiveRowBackgroundColor = active
                   };
        list.SetChoices([
            new("choice-a", "Alpha"),
            new("choice-b", "Beta")
        ]);

        var root = new Screen();
        root.Add(list);

        var frame = Render(root, 24, 4);

        frame.StyleAt(0, 0).Background.ShouldBe(active);
        frame.StyleAt(1, 0).Background.ShouldBe(active);
        frame.StyleAt(2, 0).Background.ShouldBe(active);

        frame.StyleAt(0, 1).Background.ShouldNotBe(active);
    }

    [Fact]
    public void single_choice_list_uses_circle_glyphs_and_allows_only_one_selection()
    {
        var terminal = new MockTerminal();
        var list = new SingleChoiceList();
        list.SetChoices([
            new("choice-a", "Alpha", true),
            new("choice-b", "Beta", true)
        ]);

        var root = new Screen();
        root.Add(list);

        var attention = new AttentionManager(terminal, root, keyboardFocus: list);
        attention.Render();
        attention.HandleKey(new('\0', ConsoleKey.DownArrow, false, false, false));
        attention.HandleKey(new(' ', ConsoleKey.Spacebar, false, false, false));

        list.SelectedChoiceIds.Count.ShouldBe(1);
        list.SelectedChoiceIds[0].ShouldBe("choice-b");

        var frame = Render(root, 24, 4);
        frame.GlyphAt(0, 0).ShouldBe('○');
        frame.GlyphAt(0, 1).ShouldBe('◉');
    }

    [Fact]
    public void checked_marker_keeps_default_green_when_row_foreground_is_not_set()
    {
        var list = new MultipleChoiceList
                   {
                       ActiveRowBackgroundColor = new Color(10, 20, 30),
                       RowBackgroundColor = new Color(5, 7, 11)
                   };
        list.SetChoices([
            new("choice-a", "Alpha", true)
        ]);

        var root = new Screen();
        root.Add(list);

        var frame = Render(root, 20, 3);
        frame.StyleAt(0, 0).Foreground.ShouldBe(default_checked_color);
    }

    [Fact]
    public void explicit_row_foreground_overrides_checked_marker_color()
    {
        var overrideForeground = new Color(220, 190, 80);
        var list = new MultipleChoiceList
                   {
                       RowForegroundColor = overrideForeground,
                       ActiveRowBackgroundColor = new Color(9, 12, 20)
                   };
        list.SetChoices([
            new("choice-a", "Alpha", true)
        ]);

        var root = new Screen();
        root.Add(list);

        var frame = Render(root, 20, 3);
        frame.StyleAt(0, 0).Foreground.ShouldBe(overrideForeground);
    }

    readonly record struct RenderedFrame(ScreenBuffer Buffer, RenderContext Context)
    {
        public char GlyphAt(int x, int y)
        {
            var cell = Buffer.GetCell(x, y);
            return cell.GlyphId == 0 ? ' ' : (char)cell.GlyphId;
        }

        public Style StyleAt(int x, int y)
        {
            var cell = Buffer.GetCell(x, y);
            Context.Styles.TryGet(cell.StyleIndex, out var style).ShouldBeTrue();
            return style;
        }
    }

    static RenderedFrame Render(IWidget root, int width, int height)
    {
        var engine = new FrameEngine(fullRender: false);
        var uiContext = new UiContext(root);
        var (renderBuffer, _, _) = engine.RenderFrame(root,
                                                      uiContext,
                                                      width,
                                                      height,
                                                      new Dictionary<IWidget, InvalidationKind>());

        var buffer = new ScreenBuffer(width, height);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                buffer.SetCell(x, y, renderBuffer.GetCell(x, y));

        return new(buffer, new RenderContext(uiContext));
    }
}
