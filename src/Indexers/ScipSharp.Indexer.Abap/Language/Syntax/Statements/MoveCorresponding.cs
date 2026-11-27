// Ported from abaplint packages/core/src/abap/5_syntax/statements/move_corresponding.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class MoveCorresponding : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        var s = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
        var t = node.FindDirectExpression<SimpleTarget>();
        if (s == null || t == null)
        {
            var message = "MoveCorresponding, source or target not found";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }

        var sourceType = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        var targetType = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(t, input);

        if (!(Versions.ReleaseAtLeast(input.Scope.GetRelease(), Release.v740sp05)) && input.Scope.GetLanguageVersion() != LanguageVersion.Cloud && !input.Scope.GetOpenABAP())
        {
            if (sourceType is TableType && ((TableType)sourceType).IsWithHeader() == false)
            {
                var message = "MOVE-CORRESPONDING with tables possible from v740sp05";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
            else if (targetType is TableType && ((TableType)targetType).IsWithHeader() == false)
            {
                var message = "MOVE-CORRESPONDING with tables possible from v740sp05";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
        }
    }
}
