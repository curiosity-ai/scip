// Ported from abaplint packages/core/src/abap/2_statements/expressions/switch_body.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SwitchBody : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var or = Seq("OR", Expr<Source>());

        var swhen = Seq("WHEN", Expr<Source>(), Star(or), "THEN", Alt(Expr<Source>(), Expr<Throw>()));

        return Seq(
          Opt(Expr<Let>()),
          Expr<Source>(),
          Plus(swhen),
          Opt(Seq("ELSE", Alt(Expr<Source>(), Expr<Throw>()))));
    }
}
