using System.Reflection;
using Thoth.Widgets;
using Thoth.Widgets.Layout;

namespace Comptatata.Tests.App.Cli.UI.Thoth.layout.single_child_layouts;

static class single_child_layout_test_support
{
    public static WidgetSizeRequest MeasureChild(IWidgetWithLayout child, SizeConstraint constraint)
    {
        var children = new List<IWidgetWithLayout>();
        var collector = new collecting_layout_child_visitor(children);
        child.Accept(ref collector);

        var desires = new WidgetSizeRequest[children.Count];
        for (var i = 0; i < children.Count; i++)
            desires[i] = MeasureChild(children[i], constraint);

        return child.GetLayoutCreator().Measure(child, constraint, desires);
    }

    public static TChild GetField<TChild>(object owner, string name)
        where TChild : class
    {
        var field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException($"Field '{name}' was not found on {owner.GetType().Name}.");

        return field.GetValue(owner) as TChild
               ?? throw new InvalidOperationException($"Field '{name}' on {owner.GetType().Name} was not a {typeof(TChild).Name}.");
    }
}

readonly struct collecting_layout_child_visitor(List<IWidgetWithLayout> children) : IVisitor
{
    public void Visit(IWidgetWithLayout child)
    {
        children.Add(child);
    }
}

struct counting_layout_child_visitor : IVisitor
{
    public int Count;
    public IWidgetWithLayout? LastChild;

    public void Visit(IWidgetWithLayout child)
    {
        Count++;
        LastChild = child;
    }
}
