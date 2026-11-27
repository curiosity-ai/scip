// Ported from abaplint packages/core/src/abap/2_statements/expressions/read_table_target.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ReadTableTarget : Expression
{
    public override IStatementRunnable GetRunnable()
    {

        var target = AltPrio(Seq("ASSIGNING", Expr<FSTarget>()),
                               Seq(OptPrio("REFERENCE"), "INTO", Expr<Target>()),
                               "TRANSPORTING NO FIELDS");

        return target;
    }
}
