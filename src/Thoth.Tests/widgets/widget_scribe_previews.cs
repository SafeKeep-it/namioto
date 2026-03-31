using System.Runtime.CompilerServices;
using Thoth.Bindings;
using Thoth.Modal;
using Thoth.Rendering;
using Thoth.Tests.utilities;
using Thoth.Widgets;
using Thoth.Widgets.Layout;

namespace Thoth.Tests.widgets;

/// <summary>
/// Per-widget SVG previews at 80x24 (VT100 standard terminal). Run these tests to generate preview SVGs.
/// Each widget is centred horizontally and vertically inside the full terminal viewport.
/// </summary>
public class widget_scribe_previews
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

    static (ScreenBuffer Buffer, RenderContext Context, FrameLayoutState Layout) Render(IWidget root)
    {
        var uiContext = new UiContext(root);
        var buffer = new ScreenBuffer(Width, Height);
        var layout = tree_render_harness.Render(root, buffer, uiContext);
        return (buffer, new RenderContext(uiContext), layout);
    }

    static void WritePreview(ScreenBuffer buffer,
                             RenderContext context,
                             string baseName,
                             [CallerFilePath] string callerFilePath = "")
    {
        buffer.WriteTerminalSnapshotSvg($"{baseName}.preview.svg", callerFilePath: callerFilePath);
        terminal_snapshot_assertions.WriteTrueColorAnimatedSvg(
            $"{baseName}.doc.svg",
            [new terminal_snapshot_assertions.TrueColorInteractionFrame(JsonTerminal.Capture(buffer), 1000)],
            context,
            callerFilePath: callerFilePath);
    }

    [Fact]
    public void button_preview()
    {
        var root = new Screen();
        var btn = new Button { Text = "Click Me" };
        root.Add(Centered(btn));
        var (buffer, context, _) = Render(root);
        WritePreview(buffer, context, "button");
    }

    [Fact]
    public void progress_bar_preview()
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
        var (buffer, context, _) = Render(root);
        WritePreview(buffer, context, "progress_bar");
    }

    [Fact]
    public void spinner_preview()
    {
        var root = new Screen();
        var spinner = new Spinner
        {
            Dial = SpinnerDial.Braille,
            LaneWidth = 20
        };
        root.Add(Centered(spinner));
        var (buffer, context, _) = Render(root);
        WritePreview(buffer, context, "spinner");
    }

    [Fact]
    public void toggle_preview()
    {
        var root = new Screen();
        var on = new Toggle { IsChecked = true };
        var off = new Toggle { IsChecked = false };
        var panel = new StackPanel();
        panel.Items.Add(on);
        panel.Items.Add(off);
        root.Add(Centered(panel));
        var (buffer, context, _) = Render(root);
        WritePreview(buffer, context, "toggle");
    }

    [Fact]
    public void button_group_preview()
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
        var (buffer, context, _) = Render(root);
        WritePreview(buffer, context, "button_group");
    }

    [Fact]
    public void table_preview()
    {
        var root = new Screen();
        var table = new Table();
        table.AddAutoColumn();
        table.AddFillColumn();
        table.AddAutoColumn();

        var header = new TextBlock { Text = "Name" };
        var header2 = new TextBlock { Text = "Description" };
        var header3 = new TextBlock { Text = "Status" };
        table.AddRow(header, header2, header3);

        var row1a = new TextBlock { Text = "Widget A" };
        var row1b = new TextBlock { Text = "A test widget with some content" };
        var row1c = new TextBlock { Text = "Active" };
        table.AddRow(row1a, row1b, row1c);

        var row2a = new TextBlock { Text = "Widget B" };
        var row2b = new TextBlock { Text = "Another widget with more content" };
        var row2c = new TextBlock { Text = "Idle" };
        table.AddRow(row2a, row2b, row2c);

        var row3a = new TextBlock { Text = "Widget C" };
        var row3b = new TextBlock { Text = "A third widget with even more text" };
        var row3c = new TextBlock { Text = "Error" };
        table.AddRow(row3a, row3b, row3c);

        root.Add(Centered(table));
        var (buffer, context, _) = Render(root);
        WritePreview(buffer, context, "table");
    }

    [Fact]
    public void screen_preview()
    {
        var root = new Screen { Title = "Screen Preview" };
        var text = new TextBlock
        {
            Text = "This is a Screen widget. It can host multiple child widgets.",
            Overflow = TextOverflow.Wrap
        };
        root.Add(Centered(text));
        var (buffer, context, _) = Render(root);
        WritePreview(buffer, context, "screen");
    }

    [Fact]
    public void border_preview()
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
        var (buffer, context, _) = Render(root);
        WritePreview(buffer, context, "border");
    }

    [Fact]
    public void text_block_preview()
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
        var (buffer, context, _) = Render(root);
        WritePreview(buffer, context, "text_block");
    }

    [Fact]
    public void text_bar_preview()
    {
        var root = new Screen();
        var panel = new StackPanel();
        panel.Items.Add(new TextBar { LeftTitle = "Left", CenterTitle = "Center", RightTitle = "Right" });
        panel.Items.Add(new TextBar { LeftTitle = "Status: OK", Line = "═" });
        root.Add(Centered(panel));
        var (buffer, context, _) = Render(root);
        WritePreview(buffer, context, "text_bar");
    }

    [Fact]
    public void text_editor_preview()
    {
        var root = new Screen();
        var editor = new TextEditor();
        editor.Text = "Line one of the editor\nLine two with more content\nLine three";
        root.Add(Centered(editor));
        var (buffer, context, _) = Render(root);
        WritePreview(buffer, context, "text_editor");
    }

    [Fact]
    public void dock_preview()
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
        var (buffer, context, _) = Render(root);
        WritePreview(buffer, context, "dock");
    }

    [Fact]
    public void dock_panel_preview()
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
        var (buffer, context, _) = Render(root);
        WritePreview(buffer, context, "dock_panel");
    }

    [Fact]
    public void viewport_preview()
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
        var (buffer, context, _) = Render(root);
        WritePreview(buffer, context, "viewport");
    }

    [Fact]
    public void modal_dialog_preview()
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
        var (buffer, context, _) = Render(root);
        WritePreview(buffer, context, "modal_dialog");
    }

    [Fact]
    public void single_choice_list_preview()
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
        var (buffer, context, _) = Render(root);
        WritePreview(buffer, context, "single_choice_list");
    }

    [Fact]
    public void multiple_choice_list_preview()
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
        var (buffer, context, _) = Render(root);
        WritePreview(buffer, context, "multiple_choice_list");
    }

    [Fact]
    public void overlay_widget_preview()
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
        var (buffer, context, _) = Render(root);
        WritePreview(buffer, context, "overlay_widget");
    }

    [Fact]
    public void stack_panel_preview()
    {
        var root = new Screen();
        var panel = new StackPanel();
        panel.Items.Add(new TextBar { LeftTitle = "Stack Item 1" });
        panel.Items.Add(new TextBlock { Text = "A text block stacked below the bar", Overflow = TextOverflow.Wrap });
        panel.Items.Add(new TextBar { LeftTitle = "Stack Item 3" });
        panel.Items.Add(new ProgressBar { Width = 60, Progress = 0.4, Style = ProgressBarStyle.Solid });
        panel.Items.Add(new TextBar { LeftTitle = "Stack Item 5", RightTitle = "End" });
        root.Add(Centered(panel));
        var (buffer, context, _) = Render(root);
        WritePreview(buffer, context, "stack_panel");
    }
}
