namespace Thoth.Rendering;

public sealed record TerminalCellSnapshot(int X,
                                          int Y,
                                          int GlyphId,
                                          int StyleIndex,
                                          byte Width,
                                          string? Glyph);