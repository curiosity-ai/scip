// Ported from abaplint packages/core/src/abap/2_statements/expressions/reduce_next.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ReduceNext : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var calcAssign = Ver(Release.v754,
                               Alt(Seq(Tok<Tokens.WPlus>(), "="),
                                   Seq(Tok<Tokens.WDash>(), "="),
                                   "/=",
                                   "*=",
                                   "&&="), also: AlsoIn.OpenABAP);

        var fields = Seq(Expr<SimpleTarget>(), AltPrio("=", calcAssign), Expr<Source>());
        return Seq("NEXT", Plus(fields));
    }
}
