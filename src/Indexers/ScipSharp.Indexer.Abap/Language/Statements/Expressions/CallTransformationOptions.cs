// Ported from abaplint packages/core/src/abap/2_statements/expressions/call_transformation_options.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class CallTransformationOptions : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var field = Seq(Alt(Expr<Field>(), Expr<Integer>()), "=", Expr<Source>());
        return Plus(field);
    }
}
