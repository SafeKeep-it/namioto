namespace Thoth.Rendering.Text;

public sealed class GraphemePool
{
    readonly Dictionary<string, string> _values = [];
    readonly Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> _lookup;

    public GraphemePool()
    {
        _lookup = _values.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    public string Intern(ReadOnlySpan<char> grapheme)
    {
        if (_lookup.TryGetValue(grapheme, out var existing)) return existing;

        var value = new string(grapheme);
        _values[value] = value;
        return value;
    }
}
