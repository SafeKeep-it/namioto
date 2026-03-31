using Thoth.Rendering.Grid;
using Thoth.Widgets;

namespace Thoth.Rendering;

public sealed class ScribeFrameDrawStrategy : IFrameDrawStrategy
{
    UiContext? _fullRenderUiContext;
    RenderContext? _fullRenderContext;
    UiContext? _dirtyRenderUiContext;
    RenderContext? _dirtyRenderContext;

    public void Draw(IWidget root,
                     UiContext uiContext,
                     GridBuffer buffer,
                     IReadOnlyDictionary<IWidget, InvalidationKind>? invalidations,
                     ushort frameNumber,
                     FrameLayoutState layoutState)
    {
        var context = ResolveRenderContext(uiContext, invalidations);
        var childRenderer = new canvas_child_renderer(context, layoutState);
        var canvas = new Canvas(buffer,
                                new(0, 0, buffer.Width, buffer.Height),
                                context,
                                0,
                                0,
                                frameNumber,
                                childRenderer);
        DrawTree(root, canvas, context, layoutState, childRenderer);
    }

    RenderContext ResolveRenderContext(UiContext uiContext,
                                       IReadOnlyDictionary<IWidget, InvalidationKind>? invalidations)
    {
        if (invalidations is not null)
        {
            if (_dirtyRenderContext is null || !ReferenceEquals(_dirtyRenderUiContext, uiContext))
            {
                _dirtyRenderUiContext = uiContext;
                _dirtyRenderContext = new(uiContext, invalidations);
                return _dirtyRenderContext;
            }

            _dirtyRenderContext.Reset(uiContext, invalidations);
            return _dirtyRenderContext;
        }

        if (_fullRenderContext is null || !ReferenceEquals(_fullRenderUiContext, uiContext))
        {
            _fullRenderUiContext = uiContext;
            _fullRenderContext = new(uiContext);
        }

        return _fullRenderContext;
    }

    static void DrawTree(IWidget widget,
                         Canvas canvas,
                         RenderContext context,
                         FrameLayoutState layoutState,
                         canvas_child_renderer childRenderer)
    {
        if (!context.ShouldVisitNode(widget)) return;

        layoutState.Set(widget, canvas.Bounds, childRenderer.NextDrawOrder());

        var renderChildCountBefore = canvas.RenderChildCallCount;

        if (context.ShouldDrawSelf(widget))
            widget.GetScribe().Draw(canvas);

        if (canvas.RenderChildCallCount != renderChildCountBefore)
            return;

        var drawChildren = new draw_children_visitor(widget, canvas, context, layoutState, childRenderer);
        widget.VisitChildren(ref drawChildren);
    }

    sealed class canvas_child_renderer(RenderContext context, FrameLayoutState layoutState) : ICanvasChildRenderer
    {
        int _renderChildCallCount;
        int _drawOrder;

        public int RenderChildCallCount => _renderChildCallCount;

        public int NextDrawOrder() => _drawOrder++;

        public void RenderChild(IWidget parent, Canvas parentCanvas, in Canvas.ChildPlacement placement)
        {
            _renderChildCallCount++;

            if (!context.ShouldVisitNode(placement.Child)) return;
            if (!TryResolveChildCanvas(parent, placement.Rect, parentCanvas, out var childCanvas)) return;

            DrawTree(placement.Child, childCanvas, context, layoutState, this);
        }
    }

    static bool TryResolveChildCanvas(IWidget parent,
                                      Rect rect,
                                      Canvas parentCanvas,
                                      out Canvas childCanvas)
    {
        var offsetX = 0;
        var offsetY = 0;
        if (parent is IViewport viewport)
        {
            offsetX = viewport.OffsetX;
            offsetY = viewport.OffsetY;
        }

        var physicalX = parentCanvas.OffsetX + rect.X - offsetX;
        var physicalY = parentCanvas.OffsetY + rect.Y - offsetY;

        var visibleLeft = Math.Max(physicalX, parentCanvas.OffsetX);
        var visibleTop = Math.Max(physicalY, parentCanvas.OffsetY);
        var visibleRight = Math.Min(physicalX + rect.Width, parentCanvas.OffsetX + parentCanvas.Width);
        var visibleBottom = Math.Min(physicalY + rect.Height, parentCanvas.OffsetY + parentCanvas.Height);

        if (visibleRight <= visibleLeft || visibleBottom <= visibleTop)
        {
            childCanvas = default;
            return false;
        }

        var clippedRect = new Rect(visibleLeft,
                                   visibleTop,
                                   visibleRight - visibleLeft,
                                   visibleBottom - visibleTop);
        childCanvas = parentCanvas.Slice(clippedRect) with
        {
            OffsetX = visibleLeft - physicalX,
            OffsetY = visibleTop - physicalY
        };
        return true;
    }

    struct draw_children_visitor(IWidget parent,
                                 Canvas parentCanvas,
                                 RenderContext context,
                                 FrameLayoutState layoutState,
                                 canvas_child_renderer childRenderer) : IChildVisitor
    {
        public bool Visit(INode node)
        {
            if (node is not IWidget child) return true;
            if (!context.ShouldVisitNode(child)) return true;

            if (!layoutState.TryGetRect(child, out var rect)) return true;

            // Convert absolute rect to local coords relative to parentCanvas
            var localRect = new Rect(
                rect.X - (parentCanvas.Bounds.X - parentCanvas.OffsetX),
                rect.Y - (parentCanvas.Bounds.Y - parentCanvas.OffsetY),
                rect.Width,
                rect.Height);

            if (!TryResolveChildCanvas(parent, localRect, parentCanvas, out var childCanvas))
                return true;

            DrawTree(child, childCanvas, context, layoutState, childRenderer);
            return true;
        }
    }
}
