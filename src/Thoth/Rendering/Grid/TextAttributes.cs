namespace Thoth.Rendering.Grid;

[Flags]
public enum TextAttributes
{
    None = 0,
    Bold = 1 << 0,
    Italic = 1 << 1,
    Underline = 1 << 2,
    Reverse = 1 << 3,
    Dim = 1 << 4
}