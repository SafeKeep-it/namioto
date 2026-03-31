using System.Collections.Generic;
using Thoth.Widgets;

namespace Thoth.Bindings;

public sealed class ObservableCollectionTemplate<T>
{
    readonly List<RealizedEntry> _realizedEntries = [];

    public ObservableCollectionTemplate(ObservableCollection<T> source, Func<T, IWidget> template)
    {
        Source = source;
        Template = template;
    }

    public ObservableCollection<T> Source { get; }

    public Func<T, IWidget> Template { get; }

    public IReadOnlyList<IWidget> RealizedWidgets
    {
        get
        {
            if (_realizedEntries.Count == 0) return [];

            var widgets = new IWidget[_realizedEntries.Count];
            for (var i = 0; i < _realizedEntries.Count; i++)
                widgets[i] = _realizedEntries[i].Widget;

            return widgets;
        }
    }

    public void RealizeInitial(IWidget owner)
    {
        if (_realizedEntries.Count > 0) return;

        for (var i = 0; i < Source.Count; i++)
            _realizedEntries.Add(CreateEntry(Source[i], owner));
    }

    public void Reconcile(IWidget owner)
    {
        var sourceIndex = 0;

        while (sourceIndex < Source.Count && sourceIndex < _realizedEntries.Count)
        {
            if (EqualityComparer<T>.Default.Equals(Source[sourceIndex], _realizedEntries[sourceIndex].Item))
            {
                sourceIndex++;
                continue;
            }

            _realizedEntries[sourceIndex].Widget.Parent = SentinelWidget.Instance;
            _realizedEntries.RemoveAt(sourceIndex);
        }

        while (_realizedEntries.Count > Source.Count)
        {
            _realizedEntries[Source.Count].Widget.Parent = SentinelWidget.Instance;
            _realizedEntries.RemoveAt(Source.Count);
        }

        while (_realizedEntries.Count < Source.Count)
            _realizedEntries.Add(CreateEntry(Source[_realizedEntries.Count], owner));
    }

    RealizedEntry CreateEntry(T item, IWidget owner)
    {
        var widget = Template(item);
        widget.Parent = owner;
        return new(item, widget);
    }

    readonly record struct RealizedEntry(T Item, IWidget Widget);
}
