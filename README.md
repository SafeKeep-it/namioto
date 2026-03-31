# Thoth

A .NET 10 terminal UI framework. Declare widgets, compose layouts, handle events — Thoth renders to any terminal with true-color fidelity.

AOT-compiled. No reflection. Every part of the system has been optimized for performance.

---

## Widgets

| Widget | Description |
|---|---|
| `Screen` | Root widget — owns the terminal surface |
| `StackPanel` | Vertical stack layout |
| `DockPanel` | Header / footer / fill content areas |
| `Viewport` | Scrollable container |
| `Align` | Horizontal and vertical centering |
| `Border` | Bordered container with rounded, sharp, or inset styles |
| `OverlayWidget` | Composites an overlay on top of background content |
| `ModalDialog` | Titled dialog with footer buttons |
| `TextBlock` | Text display with word-wrap, clip, or marquee overflow |
| `TextEditor` | Multi-line text input with selection, word navigation, paste |
| `TextBar` | Single-line label bar with left / center / right titles |
| `Button` | Clickable button with hover and focus events |
| `ButtonGroup` | Ordered group of buttons with selection tracking |
| `Toggle` | On/off toggle control |
| `SingleChoiceList` | Single-selection list |
| `MultipleChoiceList` | Multi-selection list |
| `ProgressBar` | Fill progress indicator (solid or gradient) |
| `Spinner` | Animated spinner (ASCII, Dots, Braille dials) |

---

## Getting started

```csharp
var screen = new Screen { Title = "My App" };

var panel = new DockPanel();
panel.Add(new Dock
{
    Position = DockPosition.Top,
    Content = new TextBar { LeftTitle = "My App", RightTitle = "v1.0" }
});
panel.Add(new Dock
{
    Position = DockPosition.Fill,
    Content = new TextBlock
    {
        Text = "Hello from Thoth.",
        Overflow = TextOverflow.Wrap
    }
});

screen.Add(panel);
```

---

## Composing layouts

Widgets compose freely. Wrap anything in `Align` to center it:

```csharp
var dialog = new ModalDialog
{
    Title = "Confirm",
    Width = 50,
    Height = 10,
    Content = new TextBlock { Text = "Proceed?", Overflow = TextOverflow.Wrap }
};
dialog.FooterButtons.Add(new Button { Text = "OK" });
dialog.FooterButtons.Add(new Button { Text = "Cancel" });

screen.Add(new Align
{
    HorizontalAlignment = HorizontalAlignment.Center,
    VerticalAlignment = VerticalAlignment.Center,
    HeightSizeMode = HeightSizeMode.Fill,
    Content = dialog
});
```

---

## Events

Keyboard, mouse, text input, and paste events are routed through a single `EventDispatcher`. Focus is managed by `AttentionManager`.

```csharp
var button = new Button
{
    Text = "Submit",
    OnClick = () => Submit(),
    OnFocus = () => Highlight(),
    OnBlur = () => Unhighlight(),
    OnMouseEnter = () => ShowTooltip(),
    OnMouseLeave = () => HideTooltip()
};
```

---

## Themes

```csharp
Themes.Load("thoth");
Themes.SwitchToVariant("dark");
```

Each theme defines nine semantic color slots — `background`, `text`, `dim`, `border`, `accent`, `notify`, `success`, `warning`, `error` — with RGB, xterm-256, and ANSI fallbacks.

---

## Building

```bash
dotnet build src/Thoth.slnx
dotnet test src/Thoth.Tests/Thoth.Tests.csproj
```

Requires .NET SDK 10.0.101 (see `global.json`).

---

## License

MIT
