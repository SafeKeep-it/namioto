namespace Thoth;

internal static class HandlerRepository<T>
{
    static readonly Dictionary<object, Action<T>> _handlers = new();

    public static void Add(object builder, Action<T> action)
    {
        _handlers[builder] = _handlers.TryGetValue(builder, out var actionHandler)
            ? actionHandler + action
            : action;
    }

    public static void Publish(object thothBuilder, T message)
    {
        if (_handlers.TryGetValue(thothBuilder, out var handler))
            handler(message);
    }
}
