using Thoth.Widgets;

namespace Thoth.Rendering;

public record struct ScrollIntoViewCommand(Rect Region, IWidget Sender);