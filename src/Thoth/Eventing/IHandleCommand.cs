namespace Thoth.Eventing;

public interface IHandleCommand<T>
{
    void Handle(ICommandContext context, in T command);
}
