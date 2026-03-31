using Thoth.Rendering;
using Thoth.Rendering.Grid;
using Thoth.Widgets;

namespace comptatata.tests.app.cli.thoth.rendering.frame_engine.utilities;

public sealed class CapturingFrameDrawStrategy : IFrameDrawStrategy
{
    public IReadOnlyDictionary<IWidget, InvalidationKind>? LastInvalidations { get; private set; }
    public ushort LastFrameNumber { get; private set; }

    public void Draw(IWidget root,
                     UiContext uiContext,
                     GridBuffer buffer,
                     IReadOnlyDictionary<IWidget, InvalidationKind>? invalidations,
                     ushort frameNumber,
                     FrameLayoutState layoutState)
    {
        LastInvalidations = invalidations;
        LastFrameNumber = frameNumber;
    }
}
