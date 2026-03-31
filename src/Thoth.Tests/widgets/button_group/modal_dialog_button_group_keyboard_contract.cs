using Shouldly;
using Comptatata.Tests.App.Cli;
using Thoth;
using Thoth.Eventing;
using Thoth.Rendering;
using Thoth.Rendering.Grid;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Thoth.Tests.widgets.button_group;

public class modal_dialog_button_group_keyboard_contract
{
    [Fact]
    public void enter_uses_default_space_uses_selected_tab_moves_selection_and_default_renders_rightmost_with_gap()
    {
        var terminal = new MockTerminal();
        var root = new Screen();

        var okClicks = 0;
        var cancelClicks = 0;
        var ok = new Button { Text = "OK", OnClick = () => okClicks++ };
        var cancel = new Button { Text = "Cancel", OnClick = () => cancelClicks++ };

        var group = new ButtonGroup
                    {
                        DefaultButton = ok,
                        SelectedButton = cancel,
                        ButtonGap = 1,
                        SelectedBorderColor = new(250, 80, 80)
                    };
        group.Add(ok);
        group.Add(cancel);
        root.Add(group);

        var screen = new AttentionManager(terminal, root, keyboardFocus: group);
        screen.Render();

        screen.HandleKey(new('\t', ConsoleKey.Tab, false, false, false));
        screen.HandleKey(new(' ', ConsoleKey.Spacebar, false, false, false));
        screen.HandleKey(new('\t', ConsoleKey.Tab, false, true, false));
        screen.HandleKey(new(' ', ConsoleKey.Spacebar, false, false, false));
        screen.HandleKey(new('\r', ConsoleKey.Enter, false, false, false));

        okClicks.ShouldBe(2);
        cancelClicks.ShouldBe(1);

        var (layout, buffer, context) = Render(root, 20, 3);

        layout.TryGetRect(ok, out var okRect).ShouldBeTrue();
        layout.TryGetRect(cancel, out var cancelRect).ShouldBeTrue();

        okRect.X.ShouldBe(cancelRect.X + cancelRect.Width + 1);
        okRect.X.ShouldBeGreaterThan(cancelRect.X);

        var selectedStyle = StyleAt(buffer, context, cancelRect.X, cancelRect.Y);
        selectedStyle.Foreground.ShouldBe(new Color(250, 80, 80));

        buffer.WriteTerminalSnapshotSvg("modal_dialog_button_group_keyboard_contract.default_rightmost.svg");
        buffer.WriteLayoutDebugSvg(root,
                                   20,
                                   3,
                                   "modal_dialog_button_group_keyboard_contract.default_rightmost.svg",
                                   layout);
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

    static Style StyleAt(ScreenBuffer buffer, RenderContext context, int x, int y)
    {
        var cell = buffer.GetCell(x, y);
        context.Styles.TryGet(cell.StyleIndex, out var style).ShouldBeTrue();
        return style;
    }
}
