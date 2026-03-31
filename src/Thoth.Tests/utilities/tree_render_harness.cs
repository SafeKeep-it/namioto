using Thoth.Rendering;
using Thoth.Terminal.Raw.Egress;
using Thoth.Widgets;

namespace Thoth.Tests.utilities;

public static class tree_render_harness
{
    public static FrameLayoutState Render(IWidget root, ScreenBuffer buffer, UiContext? uiContext = null)
    {
        uiContext ??= new(root);
        var drawStrategy = new ScribeFrameDrawStrategy();
        var engine = new FrameEngine(fullRender: false, drawStrategy: drawStrategy);
        var (renderBuffer, _, _) = engine.RenderFrame(root,
                                                     uiContext,
                                                     buffer.Width,
                                                     buffer.Height,
                                                     new Dictionary<IWidget, InvalidationKind>());

        for (var y = 0; y < buffer.Height; y++)
            for (var x = 0; x < buffer.Width; x++)
                buffer.SetCell(x, y, renderBuffer.GetCell(x, y));

        return engine.LayoutState;
    }
}
