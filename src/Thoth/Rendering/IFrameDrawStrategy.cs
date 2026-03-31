using Thoth.Rendering.Grid;
using Thoth.Widgets;

namespace Thoth.Rendering;

public interface IFrameDrawStrategy
{
    void Draw(IWidget root,
              UiContext uiContext,
              GridBuffer buffer,
              IReadOnlyDictionary<IWidget, InvalidationKind>? invalidations,
              ushort frameNumber,
              FrameLayoutState layoutState);
}
