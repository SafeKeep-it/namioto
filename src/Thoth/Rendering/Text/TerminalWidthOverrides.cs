namespace Thoth.Rendering.Text;

public sealed class TerminalWidthOverrides : IWidthProvider
{
    readonly IWidthProvider _inner;
    readonly Dictionary<string, byte>.AlternateLookup<ReadOnlySpan<char>> _lookup;

    public TerminalWidthOverrides(Dictionary<string, byte> exceptions, IWidthProvider inner)
    {
        _lookup = exceptions.GetAlternateLookup<ReadOnlySpan<char>>();
        _inner = inner;
    }

    public byte GetWidth(ReadOnlySpan<char> cluster) =>
        _lookup.TryGetValue(cluster, out var w) ? w : _inner.GetWidth(cluster);
}