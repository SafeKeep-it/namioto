namespace Thoth.Widgets;

public interface IWidget : INode
{
    new IWidget Parent { get; set; }

    INode INode.Parent => Parent;

    new void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
    {
    }

    IWidgetRenderer GetRenderer();

    IWidgetScribe GetScribe();
}
