namespace Thoth.Rendering;

public record struct Color(byte R, byte G, byte B)
{
    public int Xterm256 => ColorQuantizer.ToXterm256(this);

    public int Ansi16 => ColorQuantizer.ToAnsi16(this);

    public int Ansi8 => ColorQuantizer.ToAnsi8(this);

    public string Ansi16Name => ColorQuantizer.Ansi16Name(Ansi16);

    public string Ansi8Name => ColorQuantizer.Ansi8Name(Ansi8);

    public static Color White => new(255, 255, 255);
    public static Color Black => new(0, 0, 0);
    public static Color Gray => new(50, 50, 50);
}
