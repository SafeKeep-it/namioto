using System.Collections.ObjectModel;
using Thoth.Rendering;

namespace Thoth.Widgets;

public sealed class StackPanelItems : Collection<IWidget>
{
    readonly StackPanel _owner;

    internal StackPanelItems(StackPanel owner)
    {
        _owner = owner;
    }

    protected override void ClearItems()
    {
        RenderPhaseGuard.ThrowIfActive("StackPanel.Items.Clear");

        for (var i = 0; i < Count; i++)
            this[i].Parent = SentinelWidget.Instance;

        base.ClearItems();
    }

    protected override void InsertItem(int index, IWidget item)
    {
        RenderPhaseGuard.ThrowIfActive("StackPanel.Items.Add");
        item.Parent = _owner;
        base.InsertItem(index, item);
    }

    protected override void RemoveItem(int index)
    {
        RenderPhaseGuard.ThrowIfActive("StackPanel.Items.Remove");
        this[index].Parent = SentinelWidget.Instance;
        base.RemoveItem(index);
    }

    protected override void SetItem(int index, IWidget item)
    {
        RenderPhaseGuard.ThrowIfActive("StackPanel.Items.Set");
        this[index].Parent = SentinelWidget.Instance;
        item.Parent = _owner;
        base.SetItem(index, item);
    }
}
