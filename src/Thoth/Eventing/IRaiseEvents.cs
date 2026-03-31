namespace Thoth.Eventing;

public interface IRaiseEvents
{
    void RaiseEvent<T>(in T @event) where T : struct;
}