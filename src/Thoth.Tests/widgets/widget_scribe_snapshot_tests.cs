using Thoth.Bindings;
using Thoth.Modal;
using Thoth.Rendering;
using Thoth.Tests.utilities;
using Thoth.Widgets;
using Thoth.Widgets.Layout;

namespace Thoth.Tests.widgets;

/// <summary>
/// Cell-level snapshot tests for per-widget previews.
/// Each test renders the widget at 80x24 and asserts it matches the approved SVG snapshot.
/// </summary>
public class widget_scribe_snapshot_tests
{
    const int Width = 80;
    const int Height = 24;

    static Thoth.Widgets.Layout.Align Centered(IWidget content) => new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        HeightSizeMode = HeightSizeMode.Fill,
        Content = content
    };

    static ScreenBuffer Render(IWidget root)
    {
        var buffer = new ScreenBuffer(Width, Height);
        tree_render_harness.Render(root, buffer);
        return buffer;
    }

    [Fact]
    public void button_snapshot()
    {
        var root = new Screen();
        var btn = new Button { Text = "Click Me" };
        root.Add(Centered(btn));
        Render(root).ShouldMatchTerminalSnapshot("button.preview.svg");
    }

    [Fact]
    public void progress_bar_snapshot()
    {
        var root = new Screen();
        var bar = new ProgressBar
        {
            Width = 60,
            Progress = 0.65,
            Style = ProgressBarStyle.Solid,
            FillColor = new Color(80, 200, 120),
            TrackColor = new Color(40, 60, 45)
        };
        root.Add(Centered(bar));
        Render(root).ShouldMatchTerminalSnapshot("progress_bar.preview.svg");
    }

    [Fact]
    public void spinner_snapshot()
    {
        var root = new Screen();
        var spinner = new Spinner
        {
            Dial = SpinnerDial.Braille,
            LaneWidth = 20
        };
        root.Add(Centered(spinner));
        Render(root).ShouldMatchTerminalSnapshot("spinner.preview.svg");
    }

    [Fact]
    public void toggle_snapshot()
    {
        var root = new Screen();
        var on = new Toggle { IsChecked = true };
        var off = new Toggle { IsChecked = false };
        var panel = new StackPanel();
        panel.Items.Add(on);
        panel.Items.Add(off);
        root.Add(Centered(panel));
        Render(root).ShouldMatchTerminalSnapshot("toggle.preview.svg");
    }

    [Fact]
    public void button_group_snapshot()
    {
        var root = new Screen();
        var group = new ButtonGroup();
        var yes = new Button { Text = "Yes" };
        var no = new Button { Text = "No" };
        var cancel = new Button { Text = "Cancel" };
        group.Add(yes);
        group.Add(no);
        group.Add(cancel);
        group.SelectedButton = yes;
        root.Add(Centered(group));
        Render(root).ShouldMatchTerminalSnapshot("button_group.preview.svg");
    }

    [Fact]
    public void table_snapshot()
    {
        var root = new Screen();
        var table = new Table();
        table.AddAutoColumn();
        table.AddFillColumn();
        table.AddAutoColumn();

        table.AddRow(new TextBlock { Text = "Name" }, new TextBlock { Text = "Description" }, new TextBlock { Text = "Status" });
        table.AddRow(new TextBlock { Text = "Widget A" }, new TextBlock { Text = "A test widget with some content" }, new TextBlock { Text = "Active" });
        table.AddRow(new TextBlock { Text = "Widget B" }, new TextBlock { Text = "Another widget with more content" }, new TextBlock { Text = "Idle" });
        table.AddRow(new TextBlock { Text = "Widget C" }, new TextBlock { Text = "A third widget with even more text" }, new TextBlock { Text = "Error" });

        root.Add(Centered(table));
        Render(root).ShouldMatchTerminalSnapshot("table.preview.svg");
    }

    [Fact]
    public void screen_snapshot()
    {
        var root = new Screen { Title = "Screen Preview" };
        var text = new TextBlock
        {
            Text = "This is a Screen widget. It can host multiple child widgets.",
            Overflow = TextOverflow.Wrap
        };
        root.Add(Centered(text));
        Render(root).ShouldMatchTerminalSnapshot("screen.preview.svg");
    }

    [Fact]
    public void border_snapshot()
    {
        var root = new Screen();
        var border = new Border
        {
            BorderStyle = BorderStyle.Rounded,
            Content = new TextBlock
            {
                Text = "Content inside a rounded border",
                Overflow = TextOverflow.Wrap
            }
        };
        root.Add(Centered(border));
        Render(root).ShouldMatchTerminalSnapshot("border.preview.svg");
    }

    [Fact]
    public void text_block_snapshot()
    {
        var root = new Screen();
        var block = new TextBlock
        {
            Text = "TextBlock widget with word-wrapped text. " +
                   "This is a longer paragraph to demonstrate wrapping behaviour across multiple lines. " +
                   "The text wraps at word boundaries to fill the available width.",
            Overflow = TextOverflow.Wrap
        };
        root.Add(Centered(block));
        Render(root).ShouldMatchTerminalSnapshot("text_block.preview.svg");
    }

    [Fact]
    public void text_bar_snapshot()
    {
        var root = new Screen();
        var panel = new StackPanel();
        panel.Items.Add(new TextBar { LeftTitle = "Left", CenterTitle = "Center", RightTitle = "Right" });
        panel.Items.Add(new TextBar { LeftTitle = "Status: OK", Line = "═" });
        root.Add(Centered(panel));
        Render(root).ShouldMatchTerminalSnapshot("text_bar.preview.svg");
    }

    [Fact]
    public void text_editor_snapshot()
    {
        var root = new Screen();
        var editor = new TextEditor();
        editor.Text = "Line one of the editor\nLine two with more content\nLine three";
        root.Add(Centered(editor));
        Render(root).ShouldMatchTerminalSnapshot("text_editor.preview.svg");
    }

    [Fact]
    public void dock_snapshot()
    {
        var root = new Screen();
        var dock = new Dock
        {
            Position = DockPosition.Fill,
            Content = new TextBlock
            {
                Text = "Dock Fill content area",
                Overflow = TextOverflow.Wrap
            }
        };
        root.Add(Centered(dock));
        Render(root).ShouldMatchTerminalSnapshot("dock.preview.svg");
    }

    [Fact]
    public void dock_panel_snapshot()
    {
        var root = new Screen();
        var panel = new DockPanel();
        panel.Add(new Dock
        {
            Position = DockPosition.Top,
            Content = new TextBar { LeftTitle = "Top Bar", CenterTitle = "DockPanel Preview" }
        });
        panel.Add(new Dock
        {
            Position = DockPosition.Bottom,
            Content = new TextBar { LeftTitle = "Bottom Bar", RightTitle = "Ready" }
        });
        panel.Add(new Dock
        {
            Position = DockPosition.Fill,
            Content = new TextBlock
            {
                Text = "Fill area: main content goes here",
                Overflow = TextOverflow.Wrap
            }
        });
        root.Add(Centered(panel));
        Render(root).ShouldMatchTerminalSnapshot("dock_panel.preview.svg");
    }

    [Fact]
    public void viewport_snapshot()
    {
        var root = new Screen();
        var innerPanel = new StackPanel();
        for (var i = 1; i <= 30; i++)
            innerPanel.Items.Add(new TextBlock { Text = $"Line {i:00}: viewport scrollable content row" });
        var viewport = new Viewport
        {
            Content = innerPanel,
            OffsetY = 5
        };
        root.Add(Centered(viewport));
        Render(root).ShouldMatchTerminalSnapshot("viewport.preview.svg");
    }

    [Fact]
    public void modal_dialog_snapshot()
    {
        var root = new Screen();
        var dialog = new ModalDialog
        {
            Title = "Confirm Action",
            Width = 50,
            Height = 12,
            Content = new TextBlock
            {
                Text = "Are you sure you want to proceed with this action?",
                Overflow = TextOverflow.Wrap
            }
        };
        dialog.FooterButtons.Add(new Button { Text = "OK" });
        dialog.FooterButtons.Add(new Button { Text = "Cancel" });
        root.Add(Centered(dialog));
        Render(root).ShouldMatchTerminalSnapshot("modal_dialog.preview.svg");
    }

    [Fact]
    public void single_choice_list_snapshot()
    {
        var root = new Screen();
        var list = new SingleChoiceList
        {
            RowBackgroundColor = new Color(30, 30, 40),
            ActiveRowBackgroundColor = new Color(50, 50, 80)
        };
        list.SetChoices([
            new ModalDialogChoice("opt1", "Option One", IsChecked: true),
            new ModalDialogChoice("opt2", "Option Two"),
            new ModalDialogChoice("opt3", "Option Three"),
            new ModalDialogChoice("opt4", "Option Four")
        ]);
        root.Add(Centered(list));
        Render(root).ShouldMatchTerminalSnapshot("single_choice_list.preview.svg");
    }

    [Fact]
    public void multiple_choice_list_snapshot()
    {
        var root = new Screen();
        var list = new MultipleChoiceList
        {
            RowBackgroundColor = new Color(30, 30, 40),
            ActiveRowBackgroundColor = new Color(50, 50, 80)
        };
        list.SetChoices([
            new ModalDialogChoice("opt1", "Option One", IsChecked: true),
            new ModalDialogChoice("opt2", "Option Two", IsChecked: true),
            new ModalDialogChoice("opt3", "Option Three"),
            new ModalDialogChoice("opt4", "Option Four")
        ]);
        root.Add(Centered(list));
        Render(root).ShouldMatchTerminalSnapshot("multiple_choice_list.preview.svg");
    }

    [Fact]
    public void overlay_widget_snapshot()
    {
        var content = new TextBlock
        {
            Text = "Background content behind the overlay",
            Overflow = TextOverflow.Wrap
        };
        var overlay = new ModalDialog
        {
            Title = "Overlay Dialog",
            Width = 40,
            Height = 10,
            Content = new TextBlock { Text = "Overlay content visible on top", Overflow = TextOverflow.Wrap }
        };
        overlay.FooterButtons.Add(new Button { Text = "Close" });
        var overlayWidget = new OverlayWidget { Content = content };
        overlayWidget.Show(overlay);
        var root = new Screen();
        root.Add(Centered(overlayWidget));
        Render(root).ShouldMatchTerminalSnapshot("overlay_widget.preview.svg");
    }

    [Fact]
    public void stack_panel_snapshot()
    {
        var root = new Screen();
        var panel = new StackPanel();
        panel.Items.Add(new TextBar { LeftTitle = "Stack Item 1" });
        panel.Items.Add(new TextBlock { Text = "A text block stacked below the bar", Overflow = TextOverflow.Wrap });
        panel.Items.Add(new TextBar { LeftTitle = "Stack Item 3" });
        panel.Items.Add(new ProgressBar { Width = 60, Progress = 0.4, Style = ProgressBarStyle.Solid });
        panel.Items.Add(new TextBar { LeftTitle = "Stack Item 5", RightTitle = "End" });
        root.Add(Centered(panel));
        Render(root).ShouldMatchTerminalSnapshot("stack_panel.preview.svg");
    }
}
