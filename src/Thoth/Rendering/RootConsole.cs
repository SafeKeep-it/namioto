using Thoth.Rendering;
using Thoth.Rendering.Grid;
using Thoth.Rendering.Layout;
using Thoth.Terminal.Raw.Egress;

namespace Thoth.Widgets;

public class RootConsole(TerminalScribe scribe, UiContext uiContext)
{
    static readonly bool FullRender =
        string.Equals(Environment.GetEnvironmentVariable("THOTH_RENDER_MODE")?.Trim(),
                      "full",
                      StringComparison.OrdinalIgnoreCase);

    readonly FrameEngine _frameEngine = new(fullRender: FullRender);
    readonly RenderContext _terminalRenderContext = new(uiContext);

    public FrameLayoutState LayoutState => _frameEngine.LayoutState;

    public void Render(IReadOnlyDictionary<IWidget, InvalidationKind> invalidations)
    {
        var width = scribe.Width;
        var height = scribe.Height;
        (var buffer, var frameNumber, var requiresFullFrame) =
            _frameEngine.RenderFrame(uiContext.Root, uiContext, width, height, invalidations);

        _terminalRenderContext.Reset(uiContext, invalidations);
        scribe.Render(buffer, _terminalRenderContext, frameNumber, requiresFullFrame);
    }

    public Size Measure(SizeConstraint constraint) =>
        new(constraint.MaxWidth, constraint.MaxHeight);

    public void Render(Canvas canvas)
    {
        uiContext.Root.GetScribe().Draw(canvas);
    }
}
