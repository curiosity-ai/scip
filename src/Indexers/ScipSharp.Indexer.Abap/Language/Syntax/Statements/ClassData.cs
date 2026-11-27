// Ported from abaplint packages/core/src/abap/5_syntax/statements/class_data.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class ClassData
{
    public TypedIdentifier? RunSyntax(StatementNode node, SyntaxInput input)
    {
        var dd = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.DataDefinition>();
        if (dd != null)
        {
            var found = global::ScipSharp.Indexer.Abap.Syntax.Expressions.DataDefinition.RunSyntax(dd, input);
            if (found == null)
            {
                return null;
            }
            if (found?.GetType().IsGeneric() == true && found?.GetType().ContainsVoid() == false)
            {
                var message = "DATA definition cannot be generic, " + found.GetName();
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return new TypedIdentifier(found.GetToken(), input.Filename, VoidType.Get(SyntaxInput.CheckSyntaxKey));
            }
            List<string> meta = new List<string>([.. found.GetMeta(), IdentifierMeta.Static]);
            return new TypedIdentifier(found.GetToken(), input.Filename, found.GetType(), meta, found.GetValue());
        }

        var fallback = node.FindFirstExpression<NamespaceSimpleName>();
        if (fallback != null)
        {
            return new TypedIdentifier(fallback.GetFirstToken(), input.Filename, new UnknownType("class data, fallback"));
        }

        return null;
    }
}
