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

    // todo, remove this method, the logic should never go up in the tree
    public StructureNode? FindParent(StatementNode node)
    {
        foreach (var child in GetChildren())
        {
            if (ReferenceEquals(child, node))
            {
                return this;
            }
            else if (child is StatementNode)
            {
                continue;
            }
            else if (child is StructureNode sn)
            {
                var res = sn.FindParent(node);
                if (res != null)
                {
                    return res;
                }
            }
        }
        return null;
    }

    public string ConcatTokens()
    {
        var concat = "";
        foreach (var child in GetChildren())
        {
            concat = concat + child.ConcatTokens();
        }
        return concat;
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
        foreach (var child in GetChildren())
        {
            if (NodeTypeCheck.Is(child.Get(), type))
            {
                return child as StatementNode;
            }
            else if (child is StatementNode)
            {
                continue;
            }
            else if (child is StructureNode sn)
            {
                var res = sn.FindFirstStatement(type);
                if (res != null)
                {
                    return res;
                }
            }
        }
        return null;
    }

    public ExpressionNode? FindFirstExpression<T>() where T : IStatementRunnable => FindFirstExpression(typeof(T));

    public ExpressionNode? FindFirstExpression(Type type)
    {
        foreach (var child in GetChildren())
        {
            var res = child.FindFirstExpression(type);
            if (res != null)
            {
                return res;
            }
        }
        return null;
    }

    public StatementNode? GetFirstStatement()
    {
        foreach (var child in GetChildren())
        {
            if (child is StatementNode sn)
            {
                return sn;
            }
            return ((StructureNode)child).GetFirstStatement();
        }
        return null;
    }

    public override AbstractToken GetFirstToken()
    {
        var child = GetFirstChild();

        if (child != null)
        {
            return child.GetFirstToken();
        }

        throw new InvalidOperationException("StructureNode, getFirstToken, unexpected type");
    }

    public override AbstractToken GetLastToken()
    {
        var child = GetLastChild();

        if (child != null)
        {
            return child.GetLastToken();
        }

        throw new InvalidOperationException("StructureNode, getLastToken, unexpected type");
    }

    public List<ExpressionNode> FindAllExpressions<T>() where T : IStatementRunnable => FindAllExpressions(typeof(T));

    public List<ExpressionNode> FindAllExpressions(Type type)
    {
        var ret = new List<ExpressionNode>();
        foreach (var child in GetChildren())
        {
            ret.AddRange(child.FindAllExpressions(type));
        }
        return ret;
    }

    public List<ExpressionNode> FindAllExpressionsRecursive<T>() where T : IStatementRunnable => FindAllExpressionsRecursive(typeof(T));

    public List<ExpressionNode> FindAllExpressionsRecursive(Type type)
    {
        var ret = new List<ExpressionNode>();
        foreach (var child in GetChildren())
        {
            ret.AddRange(child.FindAllExpressionsRecursive(type));
        }
        return ret;
    }

    /// <summary>Note: upstream StructureNode has no "recursive" flag, it is ignored here.</summary>
    public List<ExpressionNode> FindAllExpressionsMulti(Type[] type, bool recursive = false)
    {
        var ret = new List<ExpressionNode>();
        foreach (var child in GetChildren())
        {
            ret.AddRange(child.FindAllExpressionsMulti(type));
        }
        return ret;
    }

    public List<StatementNode> FindAllStatements<T>() where T : IStatement => FindAllStatements(typeof(T));

    public List<StatementNode> FindAllStatements(Type type)
    {
        var ret = new List<StatementNode>();
        foreach (var child in GetChildren())
        {
            if (child is StructureNode sn)
            {
                ret.AddRange(sn.FindAllStatements(type));
            }
            else if (child is StatementNode st && NodeTypeCheck.Is(st.Get(), type))
            {
                ret.Add(st);
            }
        }
        return ret;
    }

    public List<StatementNode> FindAllStatementNodes()
    {
        var ret = new List<StatementNode>();
        foreach (var child in GetChildren())
        {
            if (child is StatementNode st)
            {
                ret.Add(st);
            }
            else if (child is StructureNode sn)
            {
                ret.AddRange(sn.FindAllStatementNodes());
            }
        }
        return ret;
    }

    public List<StructureNode> FindAllStructuresRecursive<T>() where T : IStructure => FindAllStructuresRecursive(typeof(T));

    public List<StructureNode> FindAllStructuresRecursive(Type type)
    {
        var ret = new List<StructureNode>();

        foreach (var child in GetChildren())
        {
            if (child is not StructureNode sn)
            {
                continue;
            }
            else if (NodeTypeCheck.Is(sn.Get(), type))
            {
                ret.Add(sn);
            }
            ret.AddRange(sn.FindAllStructuresRecursive(type));
        }
        return ret;
    }

    public List<StructureNode> FindAllStructuresMulti(params Type[] type)
    {
        var ret = new List<StructureNode>();
        foreach (var t in type)
        {
            if (NodeTypeCheck.Is(Get(), t))
            {
                return [this];
            }
        }
        foreach (var child in GetChildren())
        {
            if (child is not StructureNode sn)
            {
                continue;
            }

            var found = false;
            foreach (var t in type)
            {
                // faithful to upstream, which checks this.get() here
                if (NodeTypeCheck.Is(Get(), t))
                {
                    ret.Add(sn);
                    found = true;
                }
            }
            if (!found)
            {
                ret.AddRange(sn.FindAllStructuresMulti(type));
            }
        }
        return ret;
    }

    public List<StructureNode> FindAllStructures<T>() where T : IStructure => FindAllStructures(typeof(T));

    public List<StructureNode> FindAllStructures(Type type)
    {
        var ret = new List<StructureNode>();
        if (NodeTypeCheck.Is(Get(), type))
        {
            return [this];
        }
        foreach (var child in GetChildren())
        {
            if (child is not StructureNode sn)
            {
                continue;
            }
            else if (NodeTypeCheck.Is(sn.Get(), type))
            {
                ret.Add(sn);
            }
            else
            {
                ret.AddRange(sn.FindAllStructures(type));
            }
        }
        return ret;
    }

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
        foreach (var child in GetChildren())
        {
            if (child is not StructureNode sn)
            {
                continue;
            }
            else if (NodeTypeCheck.Is(sn.Get(), type))
            {
                return sn;
            }
            else
            {
                var res = sn.FindFirstStructure(type);
                if (res != null)
                {
                    return res;
                }
            }
        }
        return null;
    }

    public override string ToString() => "StructureNode " + structure.GetType().Name;
}
