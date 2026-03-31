using Thoth.Rendering;

namespace Thoth.Themes;

public record ThemePalette(Color Background,
                           Color Foreground,
                           Color MutedText,
                           Color Separator,
                           Color Accent,
                           Color Notification,
                           Color Success,
                           Color Warning,
                           Color Error,
                           Color FocusOutline,
                           Color PanelBackground)
{
    public static ThemePalette From(Theme theme, ThemePaletteOverrides? overrides = null)
    {
        var colors = theme.Colors;

        var isDark = string.Equals(theme.Variant, "dark", StringComparison.OrdinalIgnoreCase);
        var defaultPanel = (isDark ? colors.Background.Lightness(6) : colors.Background.Lightness(-4)).Color;
        var defaultFocus = colors.Accent.Color;

        return new(overrides?.Background ?? colors.Background.Color,
                   overrides?.Foreground ?? colors.Foreground.Color,
                   overrides?.MutedText ?? colors.Dim.Color,
                   overrides?.Separator ?? colors.Border.Color,
                   overrides?.Accent ?? colors.Accent.Color,
                   overrides?.Notification ?? colors.Notify.Color,
                   overrides?.Success ?? colors.Success.Color,
                   overrides?.Warning ?? colors.Warning.Color,
                   overrides?.Error ?? colors.Error.Color,
                   overrides?.FocusOutline ?? defaultFocus,
                   overrides?.PanelBackground ?? defaultPanel);
    }
}
