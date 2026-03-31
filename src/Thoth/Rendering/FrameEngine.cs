using Thoth.Rendering.Grid;
using Thoth.Widgets;

namespace Thoth.Rendering;

public sealed class FrameEngine
{
    readonly IFrameDrawStrategy _drawStrategy;
    readonly FrameLayoutState _layoutState = new();
    readonly Dictionary<IWidget, InvalidationKind> _renderInvalidationBuffer = new();
    readonly List<Rect> _detachedRects = [];
    GridBuffer? _buffer;
    ushort _frameNumber;

    public FrameEngine(bool fullRender, IFrameDrawStrategy? drawStrategy = null)
    {
        FullRender = fullRender;
        _drawStrategy = drawStrategy ?? new ScribeFrameDrawStrategy();
    }

    public bool FullRender { get; }

    public FrameLayoutState LayoutState => _layoutState;

    public (GridBuffer Buffer, ushort FrameNumber, bool RequiresFullFrame) RenderFrame(
        IWidget root,
        UiContext uiContext,
        int width,
        int height,
        IReadOnlyDictionary<IWidget, InvalidationKind> invalidations)
    {
        var bufferResized = _buffer == null || _buffer.Width != width || _buffer.Height != height;
        if (_buffer == null)
            _buffer = new(width, height);
        else if (bufferResized)
            _buffer.Resize(width, height);

        (var frameWrapped, var frameNumber) = AdvanceFrame();
        var decision =
            FrameLayoutPlanner.Decide(FullRender, bufferResized, frameWrapped, invalidations, _renderInvalidationBuffer);

        if (decision.RequiresFullLayout)
        {
            _layoutState.BeginLayout();
            MeasurePhase(root, new(0, 0, _buffer!.Width, _buffer!.Height));
            ArrangePhase(root, new(0, 0, _buffer!.Width, _buffer!.Height));
        }
        else if (HasLayoutInvalidation(invalidations))
        {
            ArrangeInvalidatedSubtrees(invalidations);
            _layoutState.CollectDetachedRects(root, _detachedRects);
            ClearDetachedRects(_buffer!, uiContext, frameNumber, _detachedRects);
        }

        using var renderScope = RenderPhaseGuard.Enter();
        _drawStrategy.Draw(root,
                           uiContext,
                           _buffer!,
                           decision.RenderInvalidations,
                           frameNumber,
                           _layoutState);

        return (_buffer!, frameNumber, decision.RequiresFullFrame);
    }

    (bool wrapped, ushort frameNumber) AdvanceFrame()
    {
        var next = (ushort)(_frameNumber + 1);
        var wrapped = next == 0;
        if (wrapped) next = 1;
        _frameNumber = next;
        return (wrapped, next);
    }

    static void MeasurePhase(IWidget root, Rect rect)
    {
        root.GetRenderer().Measure(new(rect.Width, rect.Height));
    }

    static void ArrangePhase(IWidget root, Rect rect)
    {
        root.GetRenderer().Arrange(rect);
    }

    void ArrangeInvalidatedSubtrees(IReadOnlyDictionary<IWidget, InvalidationKind> invalidations)
    {
        foreach (var pair in invalidations)
        {
            if ((pair.Value & InvalidationKind.Layout) == 0) continue;
            if (!_layoutState.TryGetRect(pair.Key, out var rect)) continue;

            MeasurePhase(pair.Key, rect);
            ArrangePhase(pair.Key, rect);
        }
    }

    static bool HasLayoutInvalidation(IReadOnlyDictionary<IWidget, InvalidationKind> invalidations)
    {
        foreach (var pair in invalidations)
            if ((pair.Value & InvalidationKind.Layout) != 0)
                return true;

        return false;
    }

    static void ClearDetachedRects(GridBuffer buffer,
                                   UiContext uiContext,
                                   ushort frameNumber,
                                   List<Rect> detachedRects)
    {
        var context = new RenderContext(uiContext);
        var canvas = new Canvas(buffer,
                                new(0, 0, buffer.Width, buffer.Height),
                                context,
                                frameNumber: frameNumber);

        for (var i = 0; i < detachedRects.Count; i++)
        {
            var rect = detachedRects[i];
            canvas.ClearRect(rect.X, rect.Y, rect.Width, rect.Height, new Style());
        }
    }


}
