namespace Thoth.Eventing;

public interface ICapture<T> where T : struct
{
    void Capture(IEventContext context, in T @event);
}