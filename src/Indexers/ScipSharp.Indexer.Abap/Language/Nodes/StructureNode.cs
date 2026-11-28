// Ported from abaplint packages/core/src/abap/nodes/structure_node.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Statements;
using ScipSharp.Indexer.Abap.Structures.Structures;


namespace ScipSharp.Indexer.Abap.Nodes;

using Type = System.Type;

public class StructureNode : AbstractNode<IStructureOrStatementNode>, IStructureOrStatementNode
{
    private readonly IStructure structure;

    public StructureNode(IStructure structure)
    {
        this.structure = structure;
    }

    public override IStructure Get() => structure;

    public StructureNode SetChildren(IEnumerable<IStructureOrStatementNode> children)
    {
        SetChildrenInternal(children);
        return this;
    }

    // Not in abaplint: the walks below over nested structures (IF in IF in ...) use an explicit stack instead of
    // recursion, see NodeWalk; they visit the nodes in the same order as the recursive versions.

    /// <summary>The nodes below this one in pre-order; the children of a structure only when <paramref name="descend"/> says so.</summary>
    private IEnumerable<IStructureOrStatementNode> Descendants(Func<StructureNode, bool>? descend = null)
    {
        var stack = new Stack<IStructureOrStatementNode>();
        PushChildren(stack, this);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            if (node is StructureNode sn && (descend == null || descend(sn)))
            {
                PushChildren(stack, sn);
            }
        }
    }

    private static void PushChildren(Stack<IStructureOrStatementNode> stack, StructureNode node)
    {
        var children = node.GetChildren();
        for (var i = children.Count - 1; i >= 0; i--)
        {
            stack.Push(children[i]);
        }
    }

    /// <summary>The statements below this node, in order.</summary>
    private IEnumerable<StatementNode> Statements() => Descendants().OfType<StatementNode>();

    // todo, remove this method, the logic should never go up in the tree
    public StructureNode? FindParent(StatementNode node)
    {
        foreach (var child in Descendants())
        {
            if (child is StructureNode sn && sn.GetChildren().Any(c => ReferenceEquals(c, node)))
            {
                return sn;
            }
        }
        return GetChildren().Any(c => ReferenceEquals(c, node)) ? this : null;
    }

    public string ConcatTokens()
    {
        var concat = new System.Text.StringBuilder();
        foreach (var statement in Statements())
        {
            concat.Append(statement.ConcatTokens());
        }
        return concat.ToString();
    }

    public StatementNode? FindDirectStatement<T>() where T : IStatement => FindDirectStatement(typeof(T));

    public StatementNode? FindDirectStatement(Type type)
    {
        foreach (var child in GetChildren())
        {
            if (child is StatementNode sn && NodeTypeCheck.Is(sn.Get(), type))
            {
                return sn;
            }
        }
        return null;
    }

    public List<StatementNode> FindDirectStatements<T>() where T : IStatement => FindDirectStatements(typeof(T));

    public List<StatementNode> FindDirectStatements(Type type)
    {
        var ret = new List<StatementNode>();
        foreach (var child in GetChildren())
        {
            if (child is StatementNode sn && NodeTypeCheck.Is(sn.Get(), type))
            {
                ret.Add(sn);
            }
        }
        return ret;
    }

    public List<StructureNode> FindDirectStructures<T>() where T : IStructure => FindDirectStructures(typeof(T));

    public List<StructureNode> FindDirectStructures(Type type)
    {
        var ret = new List<StructureNode>();
        foreach (var child in GetChildren())
        {
            if (child is StructureNode sn && NodeTypeCheck.Is(sn.Get(), type))
            {
                ret.Add(sn);
            }
        }
        return ret;
    }

    public StatementNode? FindFirstStatement<T>() where T : IStatement => FindFirstStatement(typeof(T));

    public StatementNode? FindFirstStatement(Type type)
    {
        foreach (var child in Descendants(sn => !NodeTypeCheck.Is(sn.Get(), type)))
        {
            if (NodeTypeCheck.Is(child.Get(), type))
            {
                return child as StatementNode;
            }
        }
        return null;
    }

    public ExpressionNode? FindFirstExpression<T>() where T : IStatementRunnable => FindFirstExpression(typeof(T));

    public ExpressionNode? FindFirstExpression(Type type)
    {
        foreach (var statement in Statements())
        {
            var res = statement.FindFirstExpression(type);
            if (res != null)
            {
                return res;
            }
        }
        return null;
    }

    public StatementNode? GetFirstStatement()
    {
        IStructureOrStatementNode node = this;
        while (node is StructureNode sn)
        {
            var first = sn.GetFirstChild();
            if (first == null)
            {
                return null;
            }
            node = first;
        }
        return (StatementNode)node;
    }

    public override AbstractToken GetFirstToken() => NodeWalk.FirstToken(this, "StructureNode");

    public override AbstractToken GetLastToken() => NodeWalk.LastToken(this, "StructureNode");

    public List<ExpressionNode> FindAllExpressions<T>() where T : IStatementRunnable => FindAllExpressions(typeof(T));

    public List<ExpressionNode> FindAllExpressions(Type type)
    {
        var ret = new List<ExpressionNode>();
        foreach (var statement in Statements())
        {
            ret.AddRange(statement.FindAllExpressions(type));
        }
        return ret;
    }

    public List<ExpressionNode> FindAllExpressionsRecursive<T>() where T : IStatementRunnable => FindAllExpressionsRecursive(typeof(T));

    public List<ExpressionNode> FindAllExpressionsRecursive(Type type)
    {
        var ret = new List<ExpressionNode>();
        foreach (var statement in Statements())
        {
            ret.AddRange(statement.FindAllExpressionsRecursive(type));
        }
        return ret;
    }

    /// <summary>Note: upstream StructureNode has no "recursive" flag, it is ignored here.</summary>
    public List<ExpressionNode> FindAllExpressionsMulti(Type[] type, bool recursive = false)
    {
        var ret = new List<ExpressionNode>();
        foreach (var statement in Statements())
        {
            ret.AddRange(statement.FindAllExpressionsMulti(type));
        }
        return ret;
    }

    public List<StatementNode> FindAllStatements<T>() where T : IStatement => FindAllStatements(typeof(T));

    public List<StatementNode> FindAllStatements(Type type) => Statements().Where(st => NodeTypeCheck.Is(st.Get(), type)).ToList();

    public List<StatementNode> FindAllStatementNodes() => Statements().ToList();

    public List<StructureNode> FindAllStructuresRecursive<T>() where T : IStructure => FindAllStructuresRecursive(typeof(T));

    public List<StructureNode> FindAllStructuresRecursive(Type type) =>
        Descendants().OfType<StructureNode>().Where(sn => NodeTypeCheck.Is(sn.Get(), type)).ToList();

    /// <summary>The outermost structures of one of the types: this one, or those below it, not looking inside a match.</summary>
    private List<StructureNode> FindOutermostStructures(Type[] type)
    {
        bool matches(StructureNode sn) => type.Any(t => NodeTypeCheck.Is(sn.Get(), t));
        if (matches(this))
        {
            return [this];
        }
        return Descendants(sn => !matches(sn)).OfType<StructureNode>().Where(matches).ToList();
    }

    public List<StructureNode> FindAllStructuresMulti(params Type[] type) => FindOutermostStructures(type);

    public List<StructureNode> FindAllStructures<T>() where T : IStructure => FindAllStructures(typeof(T));

    public List<StructureNode> FindAllStructures(Type type) => FindOutermostStructures([type]);

    public StructureNode? FindDirectStructure<T>() where T : IStructure => FindDirectStructure(typeof(T));

    public StructureNode? FindDirectStructure(Type type)
    {
        if (NodeTypeCheck.Is(Get(), type))
        {
            return this;
        }
        foreach (var child in GetChildren())
        {
            if (NodeTypeCheck.Is(child.Get(), type))
            {
                return child as StructureNode;
            }
        }
        return null;
    }

    public StructureNode? FindFirstStructure<T>() where T : IStructure => FindFirstStructure(typeof(T));

    public StructureNode? FindFirstStructure(Type type)
    {
        if (NodeTypeCheck.Is(Get(), type))
        {
            return this;
        }
        return Descendants().OfType<StructureNode>().FirstOrDefault(sn => NodeTypeCheck.Is(sn.Get(), type));
    }

    public override string ToString() => "StructureNode " + structure.GetType().Name;
}
