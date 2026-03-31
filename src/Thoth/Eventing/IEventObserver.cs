namespace Thoth.Eventing;

public interface IEventObserver<T> where T : struct
{
    void Observe(IEventObserverContext context, in T e);
}