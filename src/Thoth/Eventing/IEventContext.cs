using Thoth.Rendering;

namespace Thoth.Eventing;

public interface IEventContext : IRaiseEvents
{
    bool IsHandled { get; set; }

    FrameLayoutState LayoutState { get; }
    void CaptureMouse();
    void ReleaseMouse();
}

public interface ICommandContext : IRaiseEvents { }