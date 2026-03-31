using System.Globalization;
using System.Text.Json.Serialization;
using Thoth.Rendering;

namespace Thoth.Themes;

public record ThemeColor([property: JsonPropertyName("rgb")] string Rgb,
                         [property: JsonPropertyName("xterm256")]
                         int Xterm256,
                         [property: JsonPropertyName("ansi")] string Ansi,
                         [property: JsonPropertyName("description")]
                         string Description)
{
    public Color Color => ColorFromHex(Rgb);

    static Color ColorFromHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 3)
        {
            var r = byte.Parse(hex[0].ToString() + hex[0], NumberStyles.HexNumber);
            var g = byte.Parse(hex[1].ToString() + hex[1], NumberStyles.HexNumber);
            var b = byte.Parse(hex[2].ToString() + hex[2], NumberStyles.HexNumber);
            return new(r, g, b);
        }

        if (hex.Length == 6)
        {
            var r = byte.Parse(hex[..2], NumberStyles.HexNumber);
            var g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
            return new(r, g, b);
        }

        throw new ArgumentException("Invalid hex color format", nameof(hex));
    }

    public ThemeColor Hue(double degrees)
    {
        (var l, var c, var h) = ColorToOklch(Color);
        h = (h + degrees) % 360;
        if (h < 0) h += 360;
        return this with { Rgb = OklchToHex(l, c, h) };
    }

    public ThemeColor Lightness(double percentage)
    {
        (var l, var c, var h) = ColorToOklch(Color);
        l = Math.Clamp(l + percentage / 100.0, 0, 1);
        return this with { Rgb = OklchToHex(l, c, h) };
    }

    static (double L, double C, double H) ColorToOklch(Color color)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;

        // sRGB to Linear
        r = r > 0.04045 ? Math.Pow((r + 0.055) / 1.055, 2.4) : r / 12.92;
        g = g > 0.04045 ? Math.Pow((g + 0.055) / 1.055, 2.4) : g / 12.92;
        b = b > 0.04045 ? Math.Pow((b + 0.055) / 1.055, 2.4) : b / 12.92;

        var l_ = 0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b;
        var m_ = 0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b;
        var s_ = 0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b;

        var l_root = Math.Cbrt(l_);
        var m_root = Math.Cbrt(m_);
        var s_root = Math.Cbrt(s_);

        var L = 0.2104542553 * l_root + 0.7936177850 * m_root - 0.0040720403 * s_root;
        var a = 1.9779984951 * l_root - 2.4285922050 * m_root + 0.4505937099 * s_root;
        var b_ = 0.0259040371 * l_root + 0.7827717662 * m_root - 0.8086757660 * s_root;

        var C = Math.Sqrt(a * a + b_ * b_);
        var H = Math.Atan2(b_, a) * 180.0 / Math.PI;
        if (H < 0) H += 360;

        return (L, C, H);
    }

    static string OklchToHex(double L, double C, double H)
    {
        var a = C * Math.Cos(H * Math.PI / 180.0);
        var b_ = C * Math.Sin(H * Math.PI / 180.0);

        var l_root = L + 0.3963377774 * a + 0.2158037573 * b_;
        var m_root = L - 0.1055613458 * a - 0.0638541728 * b_;
        var s_root = L - 0.0894841775 * a - 1.2914855480 * b_;

        var l_ = l_root * l_root * l_root;
        var m_ = m_root * m_root * m_root;
        var s_ = s_root * s_root * s_root;

        var r = +4.0767416621 * l_ - 3.3077115913 * m_ + 0.2309699292 * s_;
        var g = -1.2684380046 * l_ + 2.6097574011 * m_ - 0.3413193965 * s_;
        var b = -0.0041960863 * l_ - 0.7034186147 * m_ + 1.7076147010 * s_;

        // Linear to sRGB
        r = r > 0.0031308 ? 1.055 * Math.Pow(r, 1.0 / 2.4) - 0.055 : 12.92 * r;
        g = g > 0.0031308 ? 1.055 * Math.Pow(g, 1.0 / 2.4) - 0.055 : 12.92 * g;
        b = b > 0.0031308 ? 1.055 * Math.Pow(b, 1.0 / 2.4) - 0.055 : 12.92 * b;

        var ri = Math.Clamp((int)Math.Round(r * 255), 0, 255);
        var gi = Math.Clamp((int)Math.Round(g * 255), 0, 255);
        var bi = Math.Clamp((int)Math.Round(b * 255), 0, 255);

        return $"#{ri:x2}{gi:x2}{bi:x2}";
    }
}