using System.Collections;
using Thoth.Eventing;
using Thoth.Widgets;

namespace Thoth.Bindings;

public class ObservableCollection<T> : IReadOnlyList<T>
{
    readonly List<T> _items = [];

    public event Action? OnChange;

    public T this[int index] => _items[index];

    public int Count => _items.Count;

    public void Add(T item)
    {
        _items.Add(item);
        NotifyChanged();
    }

    public bool Remove(T item)
    {
        var removed = _items.Remove(item);
        if (removed) NotifyChanged();
        return removed;
    }

    public void Clear()
    {
        if (_items.Count == 0) return;
        _items.Clear();
        NotifyChanged();
    }

    public ObservableCollectionTemplate<T> Template(Func<T, IWidget> template)
    {
        return new(this, template);
    }

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    void NotifyChanged()
    {
        OnChange?.Invoke();
        BindingUpdateQueue.NotifyChanged(this);
    }
}
