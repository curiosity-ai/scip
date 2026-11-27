// Ported from abaplint packages/core/src/abap/5_syntax/statements/type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Type
{
    public TypedIdentifier? RunSyntax(StatementNode node, SyntaxInput input, string? qualifiedNamePrefix = null)
    {
        var tt = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.TypeTable>();
        if (tt != null)
        {
            return global::ScipSharp.Indexer.Abap.Syntax.Expressions.TypeTable.RunSyntax(node, input, qualifiedNamePrefix);
        }

        var found = new BasicTypes(input).SimpleType(node, qualifiedNamePrefix);
        if (found != null)
        {
            if (found?.GetType().IsGeneric() == true && found?.GetType().ContainsVoid() == false)
            {
                var message = "TYPES definition cannot be generic, " + found.GetName();
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return new TypedIdentifier(found.GetToken(), input.Filename, VoidType.Get(SyntaxInput.CheckSyntaxKey));
            }

            if (input.Scope.IsAnyOO() && found.GetType() is PackedType)
            {
                var concat = JsUtils.ToUpper(node.ConcatTokens());
                if ((concat.Contains(" TYPE P ", StringComparison.Ordinal) || concat.Contains(" TYPE P.", StringComparison.Ordinal)) && concat.Contains(" DECIMALS ", StringComparison.Ordinal) == false)
                {
                    var message = "Specify DECIMALS in OO context for packed";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return new TypedIdentifier(found.GetToken(), input.Filename, VoidType.Get(SyntaxInput.CheckSyntaxKey));
                }
            }

            return found;
        }

        var fallback = node.FindFirstExpression<NamespaceSimpleName>();
        if (fallback != null)
        {
            return new TypedIdentifier(fallback.GetFirstToken(), input.Filename, new UnknownType("Type, fallback"));
        }

        return null;
    }
}
