namespace Thoth.Eventing;

internal static class EventTypeId<T>
{
    public static readonly int Id = IdGenerator.Next();
}
