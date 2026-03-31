namespace Thoth.Rendering.Grid;

public record struct Style(Color? Foreground = null,
                           Color? Background = null,
                           Color? UnderlineColor = null,
                           Rendering.TextAttributes Attributes = Rendering.TextAttributes.None,
                           string? Hyperlink = null);