// Helper for the ported semantic layer, not a port of an upstream file.
// TS calls a method on a union of node classes (StatementNode | ExpressionNode, ExpressionNode | TokenNode)
// when every member of the union has it; in C# the union is INode / IExpressionOrTokenNode, so these
// extension methods dispatch on the runtime class.
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements;

namespace ScipSharp.Indexer.Abap;

public static class NodeUnionExtensions
{
    public static ExpressionNode? FindFirstExpression<T>(this INode node) where T : IStatementRunnable => node.FindFirstExpression(typeof(T));

    public static ExpressionNode? FindFirstExpression(this INode node, Type type)
    {
        return node switch
        {
            ExpressionNode e => e.FindFirstExpression(type),
            StatementNode s => s.FindFirstExpression(type),
            StructureNode s => s.FindFirstExpression(type),
            _ => null,
        };
    }

    public static ExpressionNode? FindDirectExpression<T>(this INode node) where T : IStatementRunnable => node.FindDirectExpression(typeof(T));

    public static ExpressionNode? FindDirectExpression(this INode node, Type type)
    {
        return node switch
        {
            ExpressionNode e => e.FindDirectExpression(type),
            StatementNode s => s.FindDirectExpression(type),
            _ => null,
        };
    }

    public static List<ExpressionNode> FindDirectExpressions<T>(this INode node) where T : IStatementRunnable => node.FindDirectExpressions(typeof(T));

    public static List<ExpressionNode> FindDirectExpressions(this INode node, Type type)
    {
        return node switch
        {
            ExpressionNode e => e.FindDirectExpressions(type),
            StatementNode s => s.FindDirectExpressions(type),
            _ => new List<ExpressionNode>(),
        };
    }

    public static List<ExpressionNode> FindAllExpressions<T>(this INode node) where T : IStatementRunnable => node.FindAllExpressions(typeof(T));

    public static List<ExpressionNode> FindAllExpressions(this INode node, Type type)
    {
        return node switch
        {
            ExpressionNode e => e.FindAllExpressions(type),
            StatementNode s => s.FindAllExpressions(type),
            StructureNode s => s.FindAllExpressions(type),
            _ => new List<ExpressionNode>(),
        };
    }

    public static List<ExpressionNode> FindAllExpressionsRecursive<T>(this INode node) where T : IStatementRunnable => node.FindAllExpressionsRecursive(typeof(T));

    public static List<ExpressionNode> FindAllExpressionsRecursive(this INode node, Type type)
    {
        return node switch
        {
            ExpressionNode e => e.FindAllExpressionsRecursive(type),
            StatementNode s => s.FindAllExpressionsRecursive(type),
            StructureNode s => s.FindAllExpressionsRecursive(type),
            _ => new List<ExpressionNode>(),
        };
    }

    public static AbstractToken? FindDirectTokenByText(this INode node, string text)
    {
        return node switch
        {
            ExpressionNode e => e.FindDirectTokenByText(text),
            StatementNode s => s.FindDirectTokenByText(text),
            _ => null,
        };
    }

    public static ExpressionNode? FindExpressionAfterToken(this INode node, string text)
    {
        return node switch
        {
            ExpressionNode e => e.FindExpressionAfterToken(text),
            StatementNode s => s.FindExpressionAfterToken(text),
            _ => null,
        };
    }

    public static INode? GetFirstChild(this INode node)
    {
        var children = node.GetChildren();
        return children.Count > 0 ? children[0] : null;
    }

    public static INode? GetLastChild(this INode node)
    {
        var children = node.GetChildren();
        return children.Count > 0 ? children[children.Count - 1] : null;
    }

    public static string ConcatTokens(this INode node)
    {
        return node switch
        {
            ExpressionNode e => e.ConcatTokens(),
            StatementNode s => s.ConcatTokens(),
            StructureNode s => s.ConcatTokens(),
            TokenNode t => t.ConcatTokens(),
            _ => "",
        };
    }

    public static List<AbstractToken> GetTokens(this INode node)
    {
        return node switch
        {
            ExpressionNode e => e.GetTokens(),
            StatementNode s => s.GetTokens(),
            TokenNode t => new List<AbstractToken> { t.Get() },
            _ => new List<AbstractToken>(),
        };
    }
}
