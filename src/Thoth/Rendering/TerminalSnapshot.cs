namespace Thoth.Rendering;

public sealed record TerminalSnapshot(int Width,
                                      int Height,
                                      IReadOnlyList<TerminalCellSnapshot> Cells);