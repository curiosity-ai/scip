// Not in abaplint: iterative versions of the recursive node walks.
//
// A long ABAP expression (a = b + c + d + ..., or deeply nested method calls) parses into a tree that is as deep as it
// is long. Walking it recursively ran out of stack, which kills the process (a StackOverflowException cannot be
// caught), so the walks below use an explicit stack. Each visits the nodes in the same order as the recursive walk.
using ScipSharp.Indexer.Abap.Lexer.Tokens;

namespace ScipSharp.Indexer.Abap.Nodes;

internal static class NodeWalk
{
    /// <summary>Pushes the children of a node in reverse, so that they are popped in order.</summary>
    private static void PushChildren<T>(Stack<T> stack, IReadOnlyList<T> children)
    {
        for (var i = children.Count - 1; i >= 0; i--)
        {
            stack.Push(children[i]);
        }
    }

    /// <summary>The token nodes below the given nodes, left to right.</summary>
    public static void TokenNodes(IReadOnlyList<INode> roots, List<TokenNode> tokens)
    {
        var stack = new Stack<INode>();
        PushChildren(stack, roots);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is TokenNode tn)
            {
                tokens.Add(tn);
            }
            else
            {
                PushChildren(stack, node.GetChildren());
            }
        }
    }

    /// <summary>The tokens below the given nodes, left to right.</summary>
    public static void Tokens(IReadOnlyList<INode> roots, List<AbstractToken> tokens)
    {
        var stack = new Stack<INode>();
        PushChildren(stack, roots);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is TokenNode tn)
            {
                tokens.Add(tn.Get());
            }
            else
            {
                PushChildren(stack, node.GetChildren());
            }
        }
    }

    /// <summary>
    /// The expression nodes below the given nodes in pre-order, with the number of types of <paramref name="types"/>
    /// each one is, for which <paramref name="descend"/> says whether to look below it.
    /// </summary>
    public static void Expressions(IReadOnlyList<IExpressionOrTokenNode> roots, Type[] types, Func<int, bool> descend, List<ExpressionNode> ret)
    {
        var stack = new Stack<IExpressionOrTokenNode>();
        PushChildren(stack, roots);
        while (stack.Count > 0)
        {
            if (stack.Pop() is not ExpressionNode en)
            {
                continue;
            }
            var matches = 0;
            foreach (var t in types)
            {
                if (NodeTypeCheck.Is(en.Get(), t))
                {
                    ret.Add(en);
                    matches++;
                }
            }
            if (descend(matches))
            {
                PushChildren(stack, en.GetChildren());
            }
        }
    }

    /// <summary>The first expression node below the given nodes, in pre-order, that is of the type.</summary>
    public static ExpressionNode? FirstExpression(IReadOnlyList<IExpressionOrTokenNode> roots, Type type)
    {
        var stack = new Stack<IExpressionOrTokenNode>();
        PushChildren(stack, roots);
        while (stack.Count > 0)
        {
            if (stack.Pop() is not ExpressionNode en)
            {
                continue;
            }
            if (NodeTypeCheck.Is(en.Get(), type))
            {
                return en;
            }
            PushChildren(stack, en.GetChildren());
        }
        return null;
    }

    /// <summary>The first token of a node, following first children down to a token.</summary>
    public static AbstractToken FirstToken(INode node, string what)
    {
        while (node is not TokenNode)
        {
            var children = node.GetChildren();
            if (children.Count == 0)
            {
                throw new InvalidOperationException(what + ", getFirstToken, no children");
            }
            node = children[0];
        }
        return ((TokenNode)node).Get();
    }

    /// <summary>The last token of a node, following last children down to a token.</summary>
    public static AbstractToken LastToken(INode node, string what)
    {
        while (node is not TokenNode)
        {
            var children = node.GetChildren();
            if (children.Count == 0)
            {
                throw new InvalidOperationException(what + ", getLastToken, no children");
            }
            node = children[children.Count - 1];
        }
        return ((TokenNode)node).Get();
    }
}
