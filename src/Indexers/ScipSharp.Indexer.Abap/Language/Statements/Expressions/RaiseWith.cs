// Ported from abaplint packages/core/src/abap/2_statements/expressions/raise_with.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class RaiseWith : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var wit = Seq(Expr<SimpleSource1>(),
                        Opt(Expr<SimpleSource1>()),
                        Opt(Expr<SimpleSource1>()),
                        Opt(Expr<SimpleSource1>()));
        // todo: I guess this is from version something?
        var witComplex = Seq(Expr<Source>(),
                               Opt(Expr<Source>()),
                               Opt(Expr<Source>()),
                               Opt(Expr<Source>()));
        return Seq("WITH", AltPrio(witComplex, wit));
    }
}
