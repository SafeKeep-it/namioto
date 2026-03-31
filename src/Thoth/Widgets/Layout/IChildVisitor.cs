using Thoth.Rendering;
using Thoth.Rendering.Layout;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public interface IChildVisitor
{
    bool Visit(INode child);

    void Accept<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, allows ref struct {}
}
public interface IVisitor
{
    void Visit(IWidgetWithLayout child);
}

public interface IWidgetWithLayout : IWidget
{
    ILayoutCreator GetLayoutCreator();
    public void Walk<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IVisitor, allows ref struct
    {
        visitor.Visit(this);
        Accept(ref visitor);
    }
    void Accept<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IVisitor, allows ref struct;

}