using System.Text;
using System.Linq;
using Shouldly;
using Thoth.Rendering;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Thoth.Tests.widgets.modal_dialog;

public class modal_dialog_layout
{
    [Fact]
    public void root_composition_non_overflow_modal_keeps_footer_bottom_and_content_above()
    {
        var buffer = new ScreenBuffer(60, 20);
        var root = new Screen();
        root.Add(new fill_widget { Character = '~' });

        var body = new Viewport
                   {
                       Content = new TextBlock { Text = "Short confirmation text. Please choose.", Overflow = TextOverflow.Wrap }
                   };
        var cancel = new Button { Text = "Cancel" };
        var ok = new Button { Text = "OK" };

        var dialog = new ModalDialog
                     {
                          Width = 40,
                          Height = 10,
                          MaxHeightRatio = 2.0 / 3.0,
                          Title = "Header",
                          Content = body
                     };
        dialog.FooterButtons.Add(cancel);
        dialog.FooterButtons.Add(ok);
        dialog.FooterButtons.DefaultButton = ok;
        dialog.FooterButtons.SelectedButton = cancel;
        root.Add(dialog);

        var uiContext = new UiContext(root);
        tree_render_harness.Render(root, buffer, uiContext);
        var renderContext = new RenderContext(uiContext);
        buffer.WriteTerminalSnapshotSvg("modal_dialog.layout.non_overflow.svg");
        buffer.WriteLayoutDebugSvg(root, 60, 20, "modal_dialog.layout.non_overflow.svg");

        buffer.GetCell(10, 5).GlyphId.ShouldBe('╔');
        buffer.GetCell(49, 14).GlyphId.ShouldBe('╝');
        buffer.GetCell(0, 0).GlyphId.ShouldBe('~');

        var rows = new string[20];
        for (var y = 0; y < 20; y++)
        {
            var sb = new StringBuilder();
            for (var x = 0; x < 60; x++)
            {
                var cell = buffer.GetCell(x, y);
                sb.Append(cell.GlyphId == 0 ? ' ' : (char)cell.GlyphId);
            }

            rows[y] = sb.ToString();
        }

        rows.Any(r => r.Contains("Short")).ShouldBeTrue();
        rows.Any(r => r.Contains("Cancel")).ShouldBeTrue();
        rows.Any(r => r.Contains("OK")).ShouldBeTrue();
        rows.Any(r => r.Contains("Header")).ShouldBeTrue();
        rows.Any(r => r.Contains("×")).ShouldBeFalse();

        buffer.GetCell(27, 6).GlyphId.ShouldBe('H');
        var titleStyle = style_at(buffer, renderContext, 27, 6);
        titleStyle.Attributes.HasFlag(TextAttributes.Bold).ShouldBeTrue();
    }

    [Fact]
    public void root_composition_overflow_modal_clamps_width_and_height_and_body_scrolls_inside_viewport()
    {
        var buffer = new ScreenBuffer(30, 12);
        var root = new Screen();
        root.Add(new fill_widget { Character = '`' });

        var longText = "This is a very long modal message intended to overflow vertically in a small terminal window. "
                       + "The viewport should clip and keep content inside the dialog body while footer remains docked.";
        var body = new Viewport
                   {
                       Content = new TextBlock { Text = longText, Overflow = TextOverflow.Wrap }
                   };

        var dialog = new ModalDialog
                     {
                           Width = 26,
                           Height = 40,
                           MaxWidthRatio = 2.0 / 3.0,
                           MaxHeightRatio = 2.0 / 3.0,
                           Title = "Warning",
                           Content = body
                     };
        dialog.FooterButtons.Add(new Button { Text = "Close" });
        root.Add(dialog);

        tree_render_harness.Render(root, buffer);
        buffer.WriteTerminalSnapshotSvg("modal_dialog.layout.overflow.svg");
        buffer.WriteLayoutDebugSvg(root, 30, 12, "modal_dialog.layout.overflow.svg");

        // 30 cols -> floor(30 * 2/3) = 20 and 12 rows -> floor(12 * 2/3) = 8, centered at (x=5,y=2).
        buffer.GetCell(5, 2).GlyphId.ShouldBe('╔');
        buffer.GetCell(24, 9).GlyphId.ShouldBe('╝');
        buffer.GetCell(0, 0).GlyphId.ShouldBe('`');
        buffer.GetCell(0, 11).GlyphId.ShouldBe('`');

        var visibleTextGlyphs = 0;
        for (var y = 3; y < 9; y++)
            for (var x = 6; x < 24; x++)
                if (buffer.GetCell(x, y).GlyphId != 0 && (char)buffer.GetCell(x, y).GlyphId != ' ')
                    visibleTextGlyphs++;

        visibleTextGlyphs.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void root_composition_modal_can_hide_footer_buttons_and_still_render_body()
    {
        var buffer = new ScreenBuffer(50, 16);
        var root = new Screen();
        root.Add(new fill_widget { Character = '.' });

        var body = new Viewport
                   {
                       Content = new TextBlock
                                 {
                                     Text = "Body-only modal still renders wrapped content.",
                                     Overflow = TextOverflow.Wrap
                                 }
                   };

        var dialog = new ModalDialog
                     {
                          Width = 34,
                          Height = 9,
                          MaxHeightRatio = 2.0 / 3.0,
                          Mandatory = false,
                          Title = "Notice",
                          FooterVisible = false,
                          Content = body
                     };
        dialog.FooterButtons.Add(new Button { Text = "Hidden" });
        root.Add(dialog);

        var uiContext = new UiContext(root);
        tree_render_harness.Render(root, buffer, uiContext);
        var renderContext = new RenderContext(uiContext);
        buffer.WriteTerminalSnapshotSvg("modal_dialog.layout.footer_hidden.svg");
        buffer.WriteLayoutDebugSvg(root, 50, 16, "modal_dialog.layout.footer_hidden.svg");

        var rows = new string[16];
        for (var y = 0; y < 16; y++)
        {
            var sb = new StringBuilder();
            for (var x = 0; x < 50; x++)
            {
                var cell = buffer.GetCell(x, y);
                sb.Append(cell.GlyphId == 0 ? ' ' : (char)cell.GlyphId);
            }

            rows[y] = sb.ToString();
        }

        rows.Any(r => r.Contains("Body-only")).ShouldBeTrue();
        rows.Any(r => r.Contains("Hidden")).ShouldBeFalse();
        rows.Any(r => r.Contains("×")).ShouldBeTrue();

        var closeX = -1;
        var closeY = -1;
        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 50; x++)
            {
                if (buffer.GetCell(x, y).GlyphId != '×') continue;
                closeX = x;
                closeY = y;
                break;
            }

            if (closeX >= 0) break;
        }

        closeX.ShouldBeGreaterThanOrEqualTo(0);
        closeY.ShouldBeGreaterThanOrEqualTo(0);
        var closeStyle = style_at(buffer, renderContext, closeX, closeY);
        closeStyle.Foreground.ShouldBe(Color.White);
        closeStyle.Background.ShouldBe(new Color(196, 52, 61));
        buffer.GetCell(0, 0).GlyphId.ShouldBe('.');
    }

    static Style style_at(ScreenBuffer buffer, RenderContext renderContext, int x, int y)
    {
        var cell = buffer.GetCell(x, y);
        renderContext.Styles.TryGet(cell.StyleIndex, out var style).ShouldBeTrue();
        return style;
    }
}

sealed class fill_widget : TestWidgetBase
{
    public char Character { get; set; } = ' ';

    public override void Render(Canvas canvas)
    {
        canvas.Fill(0, 0, canvas.Width, canvas.Height, (Rune)Character, new());
    }
}
