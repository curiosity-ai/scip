// Ported from abaplint packages/core/src/abap/2_statements/expressions/field_assignment.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class FieldAssignment : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var ret = Seq(Expr<FieldSub>(), "=", Expr<Source>());

        return ret;
    }
}
