namespace Thoth.Rendering;

public record struct Style(
    Color? Foreground = null,
    Color? Background = null,
    Color? UnderlineColor = null,
    TextAttributes Attributes = TextAttributes.None,
    string? Hyperlink = null
);
