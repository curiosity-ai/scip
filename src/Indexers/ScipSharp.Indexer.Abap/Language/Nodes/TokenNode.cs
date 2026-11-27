// Ported from abaplint packages/core/src/abap/nodes/token_node.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;

namespace ScipSharp.Indexer.Abap.Nodes;

public class TokenNode : IExpressionOrTokenNode
{
    private static readonly INode[] EMPTY_CHILDREN = [];

    private readonly AbstractToken token;

    public TokenNode(AbstractToken token)
    {
        this.token = token;
    }

    public void AddChild(INode n) => throw new InvalidOperationException("TokenNode, Method not implemented.");

    public void SetChildren(IEnumerable<INode> children) => throw new InvalidOperationException("TokenNode, Method not implemented.");

    public IReadOnlyList<INode> GetChildren() => EMPTY_CHILDREN;

    public string ConcatTokens() => token.GetStr();

    /// <summary>The token, typed. <see cref="INode.Get"/> returns the same object.</summary>
    public AbstractToken Get() => token;

    object INode.Get() => token;

    public int CountTokens() => 1;

    public AbstractToken GetFirstToken() => token;

    public AbstractToken GetLastToken() => token;

    public override string ToString() => token.ToString();
}
