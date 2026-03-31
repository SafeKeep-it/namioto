using Thoth.Bindings;
using Thoth.Eventing;
using Thoth.Eventing.Events;

namespace Thoth.Widgets;

public interface IBindingWidget : IWidget, IHandleCommand<UpdateBindingsCommand>
{
    void IHandleCommand<UpdateBindingsCommand>.Handle(ICommandContext context, in UpdateBindingsCommand command)
    {
        if ((command.Kind & BindingKind.Collection) != 0)
        {
            context.RaiseEvent(new OnLayoutChanged());
            return;
        }

        if ((command.Kind & BindingKind.Value) != 0)
            context.RaiseEvent(new OnContentChanged());
    }
}
