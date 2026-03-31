using System.Text;
using System.Text.Json;
using Thoth.Rendering.Grid;

namespace Thoth.Rendering.Snapshots;

public static class JsonTerminal
{
    public static TerminalSnapshot Capture(GridBuffer buffer)
    {
        var cells = new List<TerminalCellSnapshot>(buffer.Width * buffer.Height);

        for (var y = 0; y < buffer.Height; y++)
        {
            for (var x = 0; x < buffer.Width; x++)
            {
                var cell = buffer.GetCell(x, y);
                var glyph = ToGlyph(cell.GlyphId, cell.Width);
                cells.Add(new(x, y, cell.GlyphId, cell.StyleIndex, cell.Width, glyph));
            }
        }

        return new(buffer.Width, buffer.Height, cells);
    }

    public static string Serialize(GridBuffer buffer, bool indented = true)
    {
        var snapshot = Capture(buffer);
        return Serialize(snapshot, indented);
    }

    public static string Serialize(TerminalSnapshot snapshot, bool indented = true)
    {
        var options = new JsonSerializerOptions { WriteIndented = indented };

        return JsonSerializer.Serialize(snapshot, options);
    }

    public static void WriteToFile(GridBuffer buffer, string path, bool indented = true)
    {
        var json = Serialize(buffer, indented);
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    static string? ToGlyph(int glyphId, byte width)
    {
        if (width == 0) return null;
        if (glyphId <= 0) return null;
        if (!Rune.IsValid(glyphId)) return null;

        return new Rune(glyphId).ToString();
    }
}
