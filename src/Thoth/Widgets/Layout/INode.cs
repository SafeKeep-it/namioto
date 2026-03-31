namespace Thoth.Widgets;

public interface INode
{
    INode Parent { get; }

    void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
    {
    }
}
