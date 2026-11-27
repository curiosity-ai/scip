// Ported from abaplint packages/core/src/abap/5_syntax/statements/return.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Return : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        var source = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
        if (source != null)
        {
            var ret = input.Scope.FindReturningParameter();
            if (ret == null)
            {
                var message = "Method does not have a RETURNING parameter to assign to";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, source.GetFirstToken(), message));
                return;
            }

            input.Scope.AddReference(source.GetFirstToken(), ret, ReferenceType.DataReadReference, input.Filename);

            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(source, input, ret.GetType());
        }
    }
}
