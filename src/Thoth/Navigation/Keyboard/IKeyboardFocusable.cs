using Thoth.Eventing;

namespace Thoth.Navigation.Keyboard;

public interface IKeyboardFocusable
{
    void Focus(IEventContext context);
}
