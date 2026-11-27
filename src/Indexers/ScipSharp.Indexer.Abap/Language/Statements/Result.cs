// Ported from abaplint packages/core/src/abap/2_statements/result.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;

namespace ScipSharp.Indexer.Abap.Statements;

public sealed class Result
{
    private sealed class ResultNode(IExpressionOrTokenNode node, ResultNode? previous)
    {
        public readonly IExpressionOrTokenNode Node = node;
        public readonly ResultNode? Previous = previous;
    }

    private readonly IReadOnlyList<AbstractToken> tokens;
    private readonly int tokenIndex;
    private ResultNode? nodes;
    private int nodeCount;

    /// <param name="tokens">all tokens, from the tokenIndex = not yet matched</param>
    /// <param name="tokenIndex">index of the first unmatched token</param>
    /// <param name="nodes">matched tokens</param>
    public Result(IReadOnlyList<AbstractToken> tokens, int tokenIndex, IEnumerable<IExpressionOrTokenNode>? nodes = null)
    {
        this.tokens = tokens;
        this.tokenIndex = tokenIndex;
        nodeCount = 0;

        if (nodes != null)
        {
            SetNodes(nodes);
        }
    }

    private static Result FromChain(IReadOnlyList<AbstractToken> tokens, int tokenIndex, ResultNode? nodes, int nodeCount)
    {
        var ret = new Result(tokens, tokenIndex);
        ret.nodes = nodes;
        ret.nodeCount = nodeCount;
        return ret;
    }

    /// <summary>The next unmatched token. Only call when RemainingLength() &gt; 0.</summary>
    public AbstractToken Peek() => tokens[tokenIndex];

    /// <summary>TS <c>peek()</c> returning undefined past the end.</summary>
    public AbstractToken? PeekOrNull() => tokenIndex < tokens.Count ? tokens[tokenIndex] : null;

    public AbstractToken? PeekAt(int offset)
    {
        var i = tokenIndex + offset;
        return i >= 0 && i < tokens.Count ? tokens[i] : null;
    }

    public Result Shift(IExpressionOrTokenNode node)
    {
        return FromChain(tokens, tokenIndex + 1, new ResultNode(node, nodes), nodeCount + 1);
    }

    public Result WrapConsumed(int consumedTokens, ExpressionNode node)
    {
        var current = nodes;
        var currentCount = nodeCount;
        var children = new List<IExpressionOrTokenNode>();
        while (consumedTokens > 0)
        {
            if (current == null)
            {
                break;
            }
            children.Add(current.Node);
            consumedTokens = consumedTokens - current.Node.CountTokens();
            current = current.Previous;
            currentCount--;
        }

        children.Reverse();
        node.SetChildren(children);
        nodes = new ResultNode(node, current);
        nodeCount = currentCount + 1;
        return this;
    }

    public IExpressionOrTokenNode? PopNode()
    {
        if (nodes == null)
        {
            return null;
        }
        var ret = nodes.Node;
        nodes = nodes.Previous;
        nodeCount--;
        return ret;
    }

    public List<IExpressionOrTokenNode> GetNodes()
    {
        var ret = new IExpressionOrTokenNode[nodeCount];
        var current = nodes;

        for (var index = nodeCount - 1; index >= 0; index--)
        {
            if (current == null)
            {
                break;
            }
            ret[index] = current.Node;
            current = current.Previous;
        }

        return new List<IExpressionOrTokenNode>(ret);
    }

    public void SetNodes(IEnumerable<IExpressionOrTokenNode> n)
    {
        nodes = null;
        nodeCount = 0;
        foreach (var node in n)
        {
            nodes = new ResultNode(node, nodes);
            nodeCount++;
        }
    }

    public IReadOnlyList<AbstractToken> GetTokens() => tokens;

    public int GetTokenIndex() => tokenIndex;

    public int RemainingLength() => tokens.Count - tokenIndex;
}
