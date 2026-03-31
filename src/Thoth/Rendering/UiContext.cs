using Thoth.Rendering.Text;
using Thoth.Widgets;

namespace Thoth.Rendering;

public sealed class UiContext
{
    public UiContext(IWidget root, IWidthProvider? widthProvider = null)
    {
        if (root.Parent is not SentinelWidget)
            throw new InvalidOperationException(
                "UiContext root must be a visual tree root (Parent must be SentinelWidget.Instance).");

        Root = root;
        WidthProvider = widthProvider ?? WidthProviders.Unicode();
    }

    public InterningStore<Style> Styles { get; } = new();
    public InterningStore<string> Glyphs { get; } = new();
    public InterningStore<string> Links { get; } = new();
    public IWidthProvider WidthProvider { get; }
    public IWidget? KeyboardFocus { get; set; }
    public IWidget? MouseHover { get; set; }
    public IWidget Root { get; }
    public Queue<Action> EventQueue { get; } = new(16);
    public bool IsRendering { get; set; }
}
