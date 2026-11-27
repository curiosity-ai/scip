// Ported from abaplint packages/core/src/abap/2_statements/expressions/value_body_lines.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ValueBodyLines : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var range = Seq(OptPrio(Seq("FROM", Expr<Source>())), OptPrio(Seq("TO", Expr<Source>())));
        var lines = Seq("LINES OF", Expr<Source>(), range);
        return lines;
    }
}
