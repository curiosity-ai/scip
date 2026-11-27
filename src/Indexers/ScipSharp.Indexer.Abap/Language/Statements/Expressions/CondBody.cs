// Ported from abaplint packages/core/src/abap/2_statements/expressions/cond_body.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class CondBody : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var when = Seq("WHEN", AltPrio(Expr<Cond>(), Expr<Source>()), "THEN", AltPrio(Expr<Throw>(), Expr<Source>()));

        var elsee = Seq("ELSE", AltPrio(Expr<Throw>(), Expr<Source>()));

        return Seq(OptPrio(Expr<Let>()),
                   PlusPrio(when),
                   OptPrio(elsee));
    }
}
