namespace Thoth.Widgets.Layout;

public interface ILayoutVisitor : IVisitor;

public readonly ref struct NullLayoutVisitor : ILayoutVisitor
{
    public static NullLayoutVisitor Instance => new();

    public void Visit(IWidgetWithLayout _) { }
}

public readonly ref struct NullDrawVisitor : IVisitor
{
    public static NullDrawVisitor Instance => new();

    public void Visit(IWidgetWithLayout _) { }
}
