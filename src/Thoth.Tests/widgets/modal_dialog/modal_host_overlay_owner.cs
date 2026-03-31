using Comptatata.Tests.App.Cli;
using Shouldly;
using Thoth;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Thoth.Tests.widgets.modal_dialog;

public class modal_host_overlay_owner
{
    [Fact]
    public void when_modal_is_open_then_backdrop_blocks_pointer_events_to_background_content()
    {
        var terminal = new MockTerminal();
        var background = new click_probe_widget();
        var host = new OverlayWidget { Content = background };
        var root = new Screen();
        root.Add(host);

        var manager = new AttentionManager(terminal, root, keyboardFocus: background);
        manager.Render();
        manager.HandleMouseDown(0, 0, MouseButton.Left);
        manager.HandleMouseUp(0, 0, MouseButton.Left);
        background.ClickCount.ShouldBe(1);

        manager.SendCommand(new OverlayWidget.ShowOverlayCommand(new ModalDialog
                                                                 {
                                                                     Mandatory = true,
                                                                     Content = new TextBlock { Text = "Modal" }
                                                                 }));

        manager.Render();
        manager.HandleMouseDown(0, 0, MouseButton.Left);
        manager.HandleMouseUp(0, 0, MouseButton.Left);
        background.ClickCount.ShouldBe(1);

        manager.SendCommand(new OverlayWidget.CloseOverlayCommand());
        manager.Render();
        manager.HandleMouseDown(0, 0, MouseButton.Left);
        manager.HandleMouseUp(0, 0, MouseButton.Left);
        background.ClickCount.ShouldBe(2);
    }

    [Fact]
    public void when_modal_is_open_then_keyboard_events_are_scoped_to_modal_until_closed()
    {
        var terminal = new MockTerminal();
        var background = new key_probe_widget();
        var host = new OverlayWidget { Content = background };
        var root = new Screen();
        root.Add(host);

        var dismissCount = 0;
        var modal = new ModalDialog
                    {
                        Mandatory = false,
                        Content = new TextBlock { Text = "Modal" },
                        OnDismiss = () => dismissCount++
                    };

        var manager = new AttentionManager(terminal, root, keyboardFocus: background);
        manager.Render();
        manager.HandleKey(new('a', ConsoleKey.A, false, false, false));
        background.KeyCount.ShouldBe(1);

        manager.SendCommand(new OverlayWidget.ShowOverlayCommand(modal));
        manager.Render();
        manager.HandleKey(new('\x1b', ConsoleKey.Escape, false, false, false));
        dismissCount.ShouldBe(1);
        background.KeyCount.ShouldBe(1);

        manager.SendCommand(new OverlayWidget.CloseOverlayCommand());
        manager.Render();
        manager.HandleKey(new('b', ConsoleKey.B, false, false, false));
        background.KeyCount.ShouldBe(2);
    }

    sealed class click_probe_widget : TestWidgetBase,
                                      Thoth.Navigation.Focus.IFocusable,
                                      IEventHandler<OnMouseClick>
    {
        public int ClickCount { get; private set; }

        public void Handle(IEventContext context, in OnMouseClick e)
        {
            _ = context;
            _ = e;
            ClickCount++;
        }

        public override void Render(Canvas canvas)
        {
            _ = canvas;
        }
    }

    sealed class key_probe_widget : TestWidgetBase,
                                    Thoth.Navigation.Focus.IFocusable,
                                    IEventHandler<KeyPressedInput>
    {
        public int KeyCount { get; private set; }

        public void Handle(IEventContext context, in KeyPressedInput e)
        {
            _ = context;
            _ = e;
            KeyCount++;
        }

        public override void Render(Canvas canvas)
        {
            _ = canvas;
        }
    }
}
