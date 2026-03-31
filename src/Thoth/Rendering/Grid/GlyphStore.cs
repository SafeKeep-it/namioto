using Thoth.Rendering.Text;

namespace Thoth.Rendering.Grid;

public sealed class GlyphStore
{
    readonly Dictionary<string, int> _indices = [];
    readonly Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> _lookup;
    readonly List<string> _values = [];
    readonly IWidthProvider _widthProvider;
    readonly List<byte> _widths = [];

    public GlyphStore(IWidthProvider widthProvider)
    {
        _widthProvider = widthProvider;
        _lookup = _indices.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    public (int Index, byte Width) Intern(ReadOnlySpan<char> cluster)
    {
        if (_lookup.TryGetValue(cluster, out var index)) return (~index, _widths[index]);

        var width = _widthProvider.GetWidth(cluster);
        var key = new string(cluster);

        index = _values.Count;
        _values.Add(key);
        _widths.Add(width);
        _indices[key] = index;

        return (~index, width);
    }

    public string Get(int negatedIndex) => _values[~negatedIndex];

    public byte GetWidth(int negatedIndex) => _widths[~negatedIndex];

    public void Preload(IEnumerable<string> knownClusters)
    {
        foreach (var cluster in knownClusters) Intern(cluster.AsSpan());
    }

    public void Clear()
    {
        _values.Clear();
        _widths.Clear();
        _indices.Clear();
    }
}