using Shouldly;
using Comptatata.Tests.App.Cli;
using Thoth;
using Thoth.Rendering;
using Thoth.Rendering.Grid;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Thoth.Tests.widgets.button_group;

public class button_group_ordering_contract
{
    [Fact]
    public void without_default_buttons_render_in_insertion_order_left_to_right()
    {
        var terminal = new MockTerminal();
        var root = new Screen();

        var cancel = new Button { Text = "Cancel" };
        var save = new Button { Text = "Save" };

        var group = new ButtonGroup { ButtonGap = 1 };
        group.Add(cancel);
        group.Add(save);
        root.Add(group);

        var (layout, buffer, _) = Render(root, 30, 3);

        layout.TryGetRect(cancel, out var cancelRect).ShouldBeTrue();
        layout.TryGetRect(save, out var saveRect).ShouldBeTrue();

        cancelRect.X.ShouldBe(0);
        saveRect.X.ShouldBeGreaterThan(cancelRect.X + cancelRect.Width);

        buffer.WriteTerminalSnapshotSvg("button_group_ordering_contract.no_default.svg");
        buffer.WriteLayoutDebugSvg(root, 30, 3, "button_group_ordering_contract.no_default.svg", layout);
    }

    [Fact]
    public void with_default_button_it_renders_rightmost_non_defaults_remain_left()
    {
        var terminal = new MockTerminal();
        var root = new Screen();

        var cancel = new Button { Text = "Cancel" };
        var ok = new Button { Text = "OK" };

        var group = new ButtonGroup { DefaultButton = ok, ButtonGap = 1 };
        group.Add(cancel);
        group.Add(ok);
        root.Add(group);

        var (layout, buffer, _) = Render(root, 30, 3);

        layout.TryGetRect(cancel, out var cancelRect).ShouldBeTrue();
        layout.TryGetRect(ok, out var okRect).ShouldBeTrue();

        cancelRect.X.ShouldBe(0);
        okRect.X.ShouldBe(cancelRect.X + cancelRect.Width + 1);
        okRect.X.ShouldBeGreaterThan(cancelRect.X);

        buffer.WriteTerminalSnapshotSvg("button_group_ordering_contract.default_rightmost.svg");
        buffer.WriteLayoutDebugSvg(root, 30, 3, "button_group_ordering_contract.default_rightmost.svg", layout);
    }

    static (FrameLayoutState Layout, ScreenBuffer Buffer, RenderContext Context) Render(IWidget root, int width, int height)
    {
        var engine = new FrameRenderer(fullRender: false);
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

        return (engine.LayoutState, buffer, new(uiContext));
    }
}
