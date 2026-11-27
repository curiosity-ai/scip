// Ported from abaplint packages/core/src/abap/nodes/_inode.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Statements;

namespace ScipSharp.Indexer.Abap.Nodes;

public interface INode
{
    void AddChild(INode n);
    IReadOnlyList<INode> GetChildren();
    object Get();
    AbstractToken GetFirstToken();
    AbstractToken GetLastToken();
}

/// <summary>TS union <c>ExpressionNode | TokenNode</c>: the children of statement and expression nodes.</summary>
public interface IExpressionOrTokenNode : INode
{
    int CountTokens();
    string ConcatTokens();
}

/// <summary>TS union <c>StructureNode | StatementNode</c>: the children of structure nodes.</summary>
public interface IStructureOrStatementNode : INode
{
    string ConcatTokens();
    ExpressionNode? FindFirstExpression(Type type);
    List<ExpressionNode> FindAllExpressions(Type type);
    List<ExpressionNode> FindAllExpressionsRecursive(Type type);
    List<ExpressionNode> FindAllExpressionsMulti(Type[] type, bool recursive = false);
}

internal static class NodeTypeCheck
{
    /// <summary>TS <c>value instanceof type</c>.</summary>
    public static bool Is(object value, Type type) => type.IsInstanceOfType(value);
}
