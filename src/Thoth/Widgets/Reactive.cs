using Thoth.Bindings;

namespace Thoth.Widgets;

public sealed class Reactive<T> : Observable<T>
{
    public Reactive()
    {
    }

    public Reactive(T initial)
        : base(initial)
    {
    }

    public static implicit operator Reactive<T>(T value) => new(value);
}
