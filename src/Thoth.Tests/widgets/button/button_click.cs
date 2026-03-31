using Shouldly;
using Comptatata.Tests.App.Cli;
using Thoth;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Rendering;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Thoth.Tests.widgets.button;

public class button_click
{
    [Fact]
    public void auto_size_measure_follows_text_with_button_chrome()
    {
        var button = new Button { Text = "Notice" };

        var size = button.GetRenderer().Measure(new(100, 10));

        size.Width.ShouldBe(8);
        size.Height.ShouldBe(3);
    }

    [Fact]
    public void respects_min_width_when_text_is_shorter_than_minimum()
    {
        var button = new Button
                     {
                         Text = "OK",
                         MinWidth = 10
                     };

        var size = button.GetRenderer().Measure(new(100, 10));

        size.Width.ShouldBe(10);
        size.Height.ShouldBe(3);
    }

    [Fact]
    public void when_left_click_hits_button_then_click_handler_runs_once()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        var clickCount = 0;
        var button = new Button
                     {
                         Text = "OK",
                         OnClick = () => clickCount++
                     };
        root.Add(button);

        var screen = new AttentionManager(terminal, root, keyboardFocus: null);
        screen.Render();
        screen.HandleMouseDown(1, 0, MouseButton.Left);
        screen.HandleMouseUp(1, 0, MouseButton.Left);

        clickCount.ShouldBe(1);
    }

    [Fact]
    public void focus_transition_triggers_button_focus_and_blur_callbacks()
    {
        var terminal = new MockTerminal();
        var root = new Screen();
        var blurCount = 0;
        var focusCount = 0;

        var first = new Button
                    {
                        Text = "First",
                        OnBlur = () => blurCount++
                    };
        var second = new Button
                     {
                         Text = "Second",
                         OnFocus = () => focusCount++
                     };
        root.Add(first);
        root.Add(second);

        var screen = new AttentionManager(terminal, root, keyboardFocus: first);
        screen.Render();

        screen.HandleKey(new('\t', ConsoleKey.Tab, false, false, true));

        blurCount.ShouldBe(1);
        focusCount.ShouldBe(1);
    }

    [Fact]
    public void dispatching_mouse_enter_and_leave_triggers_button_hover_callbacks()
    {
        var enterCount = 0;
        var leaveCount = 0;
        var button = new Button
                     {
                         Text = "Hover",
                         OnMouseEnter = () => enterCount++,
                         OnMouseLeave = () => leaveCount++
                     };
        var dispatcher = new EventDispatcher();

        dispatcher.Dispatch(button, new OnMouseEnter());
        dispatcher.Dispatch(button, new OnMouseLeave());

        enterCount.ShouldBe(1);
        leaveCount.ShouldBe(1);
    }

    [Fact]
    public void explicit_button_colors_override_style_defaults_and_snapshot_is_written()
    {
        var foreground = new Color(230, 210, 40);
        var background = new Color(12, 24, 48);
        var border = new Color(200, 80, 90);
        var button = new Button
                     {
                         Text = "OK",
                         Style = new(new Color(1, 2, 3), new Color(4, 5, 6)),
                         ForegroundColor = foreground,
                         BackgroundColor = background,
                         BorderColor = border
                     };

        var frame = Render(button, width: 8, height: 3);

        frame.Buffer.WriteTerminalSnapshotSvg("button_click.explicit_colors.svg");
        frame.Buffer.WriteLayoutDebugSvg(button, 8, 3, "button_click.explicit_colors.svg");

        frame.GlyphAt(0, 0).ShouldBe('╔');
        var borderStyle = frame.StyleAt(0, 0);
        borderStyle.Foreground.ShouldBe(border);
        borderStyle.Background.ShouldBe(background);

        frame.GlyphAt(1, 1).ShouldBe('O');
        var labelStyle = frame.StyleAt(1, 1);
        labelStyle.Foreground.ShouldBe(foreground);
        labelStyle.Background.ShouldBe(background);
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

    static RenderedFrame Render(Button button, int width, int height)
    {
        var root = new Screen();
        root.Add(button);
        var uiContext = new UiContext(root);
        var buffer = new ScreenBuffer(width, height);
        tree_render_harness.Render(root, buffer, uiContext);
        var context = new RenderContext(uiContext);
        return new(buffer, context);
    }
}
