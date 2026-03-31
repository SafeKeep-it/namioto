using Thoth.Widgets;

namespace Thoth.Rendering;

public sealed class RenderContext
{
    UiContext _uiContext;
    IReadOnlyDictionary<IWidget, InvalidationKind>? _invalidations;
    readonly Dictionary<IWidget, RenderNodeAction> _nodeActions =
        new(ReferenceEqualityComparer.Instance);

    public RenderContext(UiContext uiContext,
                         IReadOnlyDictionary<IWidget, InvalidationKind> invalidations)
    {
        _uiContext = uiContext;
        _invalidations = invalidations;
        RefreshNodeActions(invalidations);
    }

    public RenderContext(UiContext uiContext)
    {
        _uiContext = uiContext;
        _invalidations = null;
    }

    public UiContext UiContext => _uiContext;
    public InterningStore<Style> Styles => _uiContext.Styles;
    public InterningStore<string> Glyphs => _uiContext.Glyphs;
    public InterningStore<string> Links => _uiContext.Links;

    public void Reset(UiContext uiContext,
                      IReadOnlyDictionary<IWidget, InvalidationKind>? invalidations)
    {
        _uiContext = uiContext;
        _invalidations = invalidations;

        if (invalidations is null)
        {
            if (_nodeActions.Count > 0)
                _nodeActions.Clear();
            return;
        }

        RefreshNodeActions(invalidations);
    }

    public bool IsInvalidated(IWidget widget)
    {
        if (_invalidations is null) return true;
        return _nodeActions.ContainsKey(widget);
    }

    public bool ShouldVisitNode(IWidget widget)
    {
        if (_invalidations is null) return true;
        return _nodeActions.TryGetValue(widget, out var action) &&
               (action & RenderNodeAction.VisitNode) != 0;
    }

    public bool ShouldDrawSelf(IWidget widget)
    {
        if (_invalidations is null) return true;
        return _nodeActions.TryGetValue(widget, out var action) &&
               (action & RenderNodeAction.DrawSelf) != 0;
    }

    public InvalidationKind GetInvalidation(IWidget widget)
    {
        if (_invalidations is null)
            return InvalidationKind.Content | InvalidationKind.Layout;
        return _invalidations.TryGetValue(widget, out var kind) ? kind : InvalidationKind.None;
    }

    void RefreshNodeActions(IReadOnlyDictionary<IWidget, InvalidationKind> invalidations)
    {
        if (_nodeActions.Count > 0)
            _nodeActions.Clear();

        if (invalidations.Count > 0)
            _nodeActions.EnsureCapacity(invalidations.Count);

        foreach (var pair in invalidations)
        {
            var action = RenderNodeAction.VisitNode;
            if ((pair.Value & (InvalidationKind.Content | InvalidationKind.Layout)) != 0)
                action |= RenderNodeAction.DrawSelf;

            if (_nodeActions.TryGetValue(pair.Key, out var existing))
                _nodeActions[pair.Key] = existing | action;
            else
                _nodeActions[pair.Key] = action;
        }
    }
}
