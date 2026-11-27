// Ported from abaplint packages/core/src/abap/5_syntax/statements/export.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Export : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        foreach (var s in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }

        foreach (var t in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(t, input);
        }

        var databaseName = node.FindExpressionAfterToken("DATABASE");
        if (databaseName != null)
        {
            var found = input.Scope.GetDDIC()?.LookupTableOrView(databaseName.ConcatTokens());
            if (found != null)
            {
                input.Scope.GetDDICReferences().AddUsing(input.Scope.GetParentObj(), new IObjectAndToken { Object = found.Object });
            }
        }
    }
}
