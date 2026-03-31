using Thoth.Rendering;

namespace Thoth.Themes;

public record ThemeControlVariants(ButtonVariant PrimaryButton,
                                   ButtonVariant SecondaryButton,
                                   ChoiceListVariant ChoiceList,
                                   ProgressBarVariant ProgressBar,
                                   ModalVariant Modal)
{
    public static ThemeControlVariants From(ThemePalette palette, bool isDark)
    {
        var activeRowBackground = isDark
            ? ShiftLightness(palette.Accent, +0.12)
            : ShiftLightness(palette.Accent, +0.55);
        var activeRowForeground = isDark ? new Color(8, 18, 42) : new Color(28, 40, 67);

        return new(new(palette.Notification,
                       ReadableTextOn(palette.Notification),
                       palette.Separator),
                   new(palette.PanelBackground,
                       palette.Foreground,
                       palette.Separator),
                   new(palette.PanelBackground,
                       palette.Foreground,
                       activeRowBackground,
                       activeRowForeground,
                       palette.Success),
                   new(palette.Notification,
                       palette.PanelBackground),
                   new(palette.PanelBackground,
                       palette.Foreground,
                       palette.Separator,
                       palette.FocusOutline));
    }

    static Color ReadableTextOn(Color color)
    {
        var luminance = (color.R * 299 + color.G * 587 + color.B * 114) / 1000;
        return luminance >= 150 ? new Color(22, 22, 28) : Color.White;
    }

    static Color ShiftLightness(Color color, double amount)
    {
        static byte BlendChannel(byte from, byte to, double t)
        {
            var value = from + ((to - from) * t);
            return (byte)Math.Clamp((int)Math.Round(value), 0, 255);
        }

        if (amount >= 0)
            return new(BlendChannel(color.R, 255, amount),
                       BlendChannel(color.G, 255, amount),
                       BlendChannel(color.B, 255, amount));

        var t = Math.Clamp(-amount, 0d, 1d);
        return new(BlendChannel(color.R, 0, t), BlendChannel(color.G, 0, t), BlendChannel(color.B, 0, t));
    }
}

public record ButtonVariant(Color Background,
                            Color Foreground,
                            Color Border);

public record ChoiceListVariant(Color RowBackground,
                                Color RowForeground,
                                Color ActiveRowBackground,
                                Color ActiveRowForeground,
                                Color CheckedForeground);

public record ProgressBarVariant(Color Fill,
                                 Color Track);

public record ModalVariant(Color PanelBackground,
                           Color PanelForeground,
                           Color Border,
                           Color FocusOutline);
