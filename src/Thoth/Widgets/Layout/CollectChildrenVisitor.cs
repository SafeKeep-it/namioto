using System.Buffers;

namespace Thoth.Widgets;

internal struct CollectChildrenVisitor(INode[] children) : IChildVisitor
{
    INode[] _children = children;
    int _count;

    public INode[] Children => _children;

    public int Count => _count;

    public bool Visit(INode child)
    {
        if (_count >= _children.Length)
            Grow();

        _children[_count++] = child;
        return true;
    }

    void Grow()
    {
        var replacement = ArrayPool<INode>.Shared.Rent(_children.Length * 2);
        Array.Copy(_children, replacement, _children.Length);
        ArrayPool<INode>.Shared.Return(_children, clearArray: true);
        _children = replacement;
    }
}
