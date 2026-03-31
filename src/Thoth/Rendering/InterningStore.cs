namespace Thoth.Rendering;

public sealed class InterningStore<T>
    where T : notnull
{
    readonly Dictionary<T, int> _indices = [];
    readonly List<T> _values = [];

    public int Intern(T value)
    {
        if (_indices.TryGetValue(value, out var index)) return index;

        index = _values.Count;
        _values.Add(value);
        _indices[value] = index;

        return index;
    }

    public T Get(int index) => _values[index];

    public bool TryGet(int index, out T value)
    {
        if ((uint)index < (uint)_values.Count)
        {
            value = _values[index];
            return true;
        }

        value = default!;
        return false;
    }

    public void Clear()
    {
        _values.Clear();
        _indices.Clear();
    }
}
