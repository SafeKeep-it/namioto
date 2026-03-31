using Thoth.Rendering;
using Thoth.Tests.utilities;
using Thoth.Widgets;
using AlignWidget = Thoth.Widgets.Layout.Align;

namespace Comptatata.Tests.App.Cli.UI.Thoth;

public class screen_history
{
    [Fact]
    public void renders_visual_tree_layout_as_snapshot()
    {
        var terminal = new MockTerminal { WindowWidth = 64, WindowHeight = 20 };
        var root = new Screen
                   {
                       Title = "Comptatata",
                       Style = new(new(255, 255, 255), new(30, 30, 40))
                   };
        var editor = new TextEditor { Style = new(Color.White, new Color(30, 30, 40)) };
        var history = new StackPanel();
        history.Items.Add(create_message("hello from agent", HorizontalAlignment.Left));
        history.Items.Add(create_message("another message", HorizontalAlignment.Left));

        var layout = new DockPanel();
        layout.Add(new Dock
                   {
                       Position = DockPosition.Top,
                       Content = new TextBar
                                 {
                                     CenterTitle = "Comptatata",
                                     Line = " ",
                                     Style = new(Color.White, new Color(50, 50, 70))
                                 }
                   });
        layout.Add(new Dock { Position = DockPosition.Fill, Content = new Viewport { Content = history } });
        layout.Add(new Dock
                   {
                       Position = DockPosition.Bottom,
                       Content = new Border
                                 {
                                     Content = new Viewport { Content = editor },
                                     Style = new(Color.Gray, new Color(30, 30, 40))
                                 },
                       MaximumHeight = 10
                   });
        root.Add(layout);

        var buffer = new ScreenBuffer(terminal.WindowWidth, terminal.WindowHeight);
        var uiContext = new UiContext(root) { KeyboardFocus = editor };
        tree_render_harness.Render(root, buffer, uiContext);

        buffer.ShouldMatchTerminalSnapshot("screen_history_layout.json");
    }

    static AlignWidget create_message(string content, HorizontalAlignment horizontalAlignment)
    {
        return new()
               {
                   Content = new Border
                             {
                                 Content = new TextBlock { Text = content },
                                 WidthSizeMode = WidthSizeMode.Content
                             },
                   HorizontalAlignment = horizontalAlignment
               };
    }
}
