namespace Thoth.Rendering.Grid;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct Cell
{
    public static readonly Cell Empty = new(0, 0, 1, 0);

    public int GlyphId;
    public int StyleIndex;
    public byte Width;
    public ushort Frame;

    public Cell(int glyphId, int styleIndex, byte width = 1, ushort frame = 0)
    {
        GlyphId = glyphId;
        StyleIndex = styleIndex;
        Width = width;
        Frame = frame;
    }
}
