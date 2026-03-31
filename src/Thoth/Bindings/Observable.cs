using System.Collections.Generic;
using Thoth.Eventing;
using Thoth.Widgets;

namespace Thoth.Bindings;

public class Observable<T>
{
    T _value = default!;

    public Observable()
    {
    }

    public Observable(T initial)
    {
        _value = initial;
    }

    public event Action<T>? OnChange;

    public static implicit operator Observable<T>(T value) => new(value);

    public static implicit operator T(Observable<T> observable) => observable._value;

    public void Set(T value)
    {
        if (EqualityComparer<T>.Default.Equals(_value, value)) return;
        _value = value;
        OnChange?.Invoke(value);
        BindingUpdateQueue.NotifyChanged(this);
    }
}
