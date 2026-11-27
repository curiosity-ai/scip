// Ported from abaplint packages/core/src/abap/nodes/_abstract_node.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;

namespace ScipSharp.Indexer.Abap.Nodes;

public abstract class AbstractNode<T> : INode where T : class, INode
{
    // shared constant, so nodes without children don't waste memory on empty lists
    private static readonly List<T> EMPTY = [];

    protected List<T> children;

    protected AbstractNode()
    {
        children = EMPTY;
    }

    public abstract object Get();
    public abstract AbstractToken GetFirstToken();
    public abstract AbstractToken GetLastToken();

    public virtual void AddChild(T n)
    {
        if (ReferenceEquals(children, EMPTY))
        {
            children = [];
        }
        children.Add(n);
    }

    void INode.AddChild(INode n) => AddChild((T)n);

    protected void SetChildrenInternal(IEnumerable<T> children)
    {
        // copy, the input is typically built incrementally
        this.children = new List<T>(children);
    }

    public IReadOnlyList<T> GetChildren() => children;

    IReadOnlyList<INode> INode.GetChildren() => children;

    public T? GetFirstChild() => children.Count > 0 ? children[0] : null;

    public T? GetLastChild() => children.Count > 0 ? children[children.Count - 1] : null;
}
