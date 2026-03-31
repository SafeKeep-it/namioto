namespace Thoth.Rendering;

internal static class ColorQuantizer
{
    static readonly (byte R, byte G, byte B)[] ansi16_palette =
    [
        (0, 0, 0),
        (128, 0, 0),
        (0, 128, 0),
        (128, 128, 0),
        (0, 0, 128),
        (128, 0, 128),
        (0, 128, 128),
        (192, 192, 192),
        (128, 128, 128),
        (255, 0, 0),
        (0, 255, 0),
        (255, 255, 0),
        (0, 0, 255),
        (255, 0, 255),
        (0, 255, 255),
        (255, 255, 255)
    ];

    static readonly string[] ansi16_names =
    [
        "black",
        "red",
        "green",
        "yellow",
        "blue",
        "magenta",
        "cyan",
        "white",
        "bright_black",
        "bright_red",
        "bright_green",
        "bright_yellow",
        "bright_blue",
        "bright_magenta",
        "bright_cyan",
        "bright_white"
    ];

    static readonly int[] cube_levels = [0, 95, 135, 175, 215, 255];

    static readonly (byte R, byte G, byte B)[] xterm256_palette = BuildXterm256Palette();

    public static int ToXterm256(Color color)
    {
        var bestIndex = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < xterm256_palette.Length; i++)
        {
            var candidate = xterm256_palette[i];
            var distance = DistanceSquared(color, candidate.R, candidate.G, candidate.B);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            bestIndex = i;
            if (distance == 0) break;
        }

        return bestIndex;
    }

    public static int ToAnsi16(Color color)
    {
        var bestIndex = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < ansi16_palette.Length; i++)
        {
            var candidate = ansi16_palette[i];
            var distance = DistanceSquared(color, candidate.R, candidate.G, candidate.B);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            bestIndex = i;
            if (distance == 0) break;
        }

        return bestIndex;
    }

    public static int ToAnsi8(Color color)
    {
        var ansi16 = ToAnsi16(color);
        return ansi16 < 8 ? ansi16 : ansi16 - 8;
    }

    public static string Ansi16Name(int index) => ansi16_names[Math.Clamp(index, 0, 15)];

    public static string Ansi8Name(int index) => ansi16_names[Math.Clamp(index, 0, 7)];

    static int DistanceSquared(Color source, byte r, byte g, byte b)
    {
        var dr = source.R - r;
        var dg = source.G - g;
        var db = source.B - b;
        return dr * dr + dg * dg + db * db;
    }

    static (byte R, byte G, byte B)[] BuildXterm256Palette()
    {
        var palette = new (byte R, byte G, byte B)[256];
        for (var i = 0; i < ansi16_palette.Length; i++)
            palette[i] = ansi16_palette[i];

        var index = 16;
        for (var r = 0; r < 6; r++)
        for (var g = 0; g < 6; g++)
        for (var b = 0; b < 6; b++)
            palette[index++] = ((byte)cube_levels[r], (byte)cube_levels[g], (byte)cube_levels[b]);

        for (var i = 0; i < 24; i++)
        {
            var gray = (byte)(8 + i * 10);
            palette[index++] = (gray, gray, gray);
        }

        return palette;
    }
}
