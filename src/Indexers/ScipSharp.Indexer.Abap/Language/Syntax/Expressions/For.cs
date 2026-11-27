// Ported from abaplint packages/core/src/abap/5_syntax/expressions/for.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class For
{
    public static bool RunSyntax(INode node, SyntaxInput input)
    {
        bool scoped = false;
        var inlineLoop = node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineLoopDefinition>();
        var inlineField = node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineFieldDefinition>();
        var groupsToken = node.FindExpressionAfterToken("GROUPS")?.GetFirstToken();
        var lett = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Let>();
        var addScope = inlineLoop.Count > 0 || inlineField.Count > 0 || lett != null || groupsToken != null;
        if (addScope)
        {
            // this scope is popped in parent expressions
            input.Scope.Push(ScopeType.For, "FOR", node.GetFirstToken().GetStart(), input.Filename);
            scoped = true;
        }

        foreach (var s in inlineLoop)
        {
            InlineLoopDefinition.RunSyntax(s, input);
        }

        foreach (var f in inlineField)
        {
            InlineFieldDefinition.RunSyntax(f, input);
        }

        if (groupsToken != null)
        {
            var type = VoidType.Get("todoGroupBy");
            var identifier = new TypedIdentifier(groupsToken, input.Filename, type, new List<string> { IdentifierMeta.InlineDefinition });
            input.Scope.AddIdentifier(identifier);
            input.Scope.AddReference(groupsToken, identifier, ReferenceType.DataWriteReference, input.Filename);
        }

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            Source.RunSyntax(s, input);
        }

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.ComponentCond>())
        {
            ComponentCond.RunSyntax(s, input);
        }

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Cond>())
        {
            Cond.RunSyntax(s, input);
        }

        if (lett != null)
        {
            Let.RunSyntax(lett, input, true);
        }

        return scoped;
    }
}
