namespace Thoth.Eventing;

public interface IEventHandler<T> where T : struct
{
    void Handle(IEventContext context, in T e);
}