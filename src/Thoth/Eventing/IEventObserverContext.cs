namespace Thoth.Eventing;

public interface IEventObserverContext : IRaiseEvents
{
    bool IsHandled { get; }
}