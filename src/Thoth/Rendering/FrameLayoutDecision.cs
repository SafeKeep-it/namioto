using Thoth.Widgets;

namespace Thoth.Rendering;

internal readonly record struct FrameLayoutDecision(bool RequiresFullFrame,
                                           bool RequiresFullLayout,
                                           IReadOnlyDictionary<IWidget, InvalidationKind>? RenderInvalidations);
