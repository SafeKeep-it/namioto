using Shouldly;
using Thoth.Rendering;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Thoth.Tests.widgets.dock_panel;

public class removing_fill_clears_stale_rect
{
    [Fact]
    public void removed_fill_region_is_cleared_on_panel_layout_change()
    {
        var panel = new DockPanel();
        var title = new fill_widget(10, 1, '-');
        var fill = new fill_widget(10, 3, 'X');
        var titleDock = new Dock { Position = DockPosition.Top, Content = title };
        var fillDock = new Dock { Position = DockPosition.Fill, Content = fill };
        panel.Add(titleDock);
        panel.Add(fillDock);

        var engine = new FrameRenderer(fullRender: false);
        var uiContext = new UiContext(panel);
        var firstFrame = engine.RenderFrame(panel, uiContext, 10, 4, new Dictionary<IWidget, InvalidationKind>());
        var firstFillGlyph = firstFrame.Buffer.GetCell(0, 1).GlyphId;

        panel.Remove(fillDock);

        var invalidations = new Dictionary<IWidget, InvalidationKind>
                            {
                                [panel] = InvalidationKind.Layout
                            };

        var secondFrame = engine.RenderFrame(panel, uiContext, 10, 4, invalidations);

        for (var y = 1; y < 4; y++)
        for (var x = 0; x < 10; x++)
        {
            var cell = secondFrame.Buffer.GetCell(x, y);
            cell.Frame.ShouldBe(secondFrame.FrameNumber);
            cell.GlyphId.ShouldBe(Cell.Empty.GlyphId);
        }

        firstFillGlyph.ShouldBe((int)'X');
    }

    sealed class fill_widget(int width, int height, char glyph) : TestWidgetBase
    {
        public override Size Measure(SizeConstraint constraint) => new(width, height);

        public override void Render(Canvas canvas)
        {
            canvas.Fill(0, 0, canvas.Width, canvas.Height, new System.Text.Rune(glyph), new Style());
        }
    }
}
