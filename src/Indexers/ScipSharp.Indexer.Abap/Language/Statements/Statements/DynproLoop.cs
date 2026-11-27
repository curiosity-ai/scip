// Ported from abaplint packages/core/src/abap/2_statements/statements/dynpro_loop.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class DynproLoop : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var into = Seq("INTO", Expr<SimpleSource2>());
        var cursor = Seq("CURSOR", Expr<SimpleSource2>());
        var withControl = Seq("WITH CONTROL", Expr<SimpleSource2>());
        var from = Seq("FROM", Expr<SimpleSource2>());
        var to = Seq("TO", Expr<SimpleSource2>());

        var at = Seq(
          "AT",
          Expr<SimpleSource2>(),
          Per(into, withControl, cursor, from, to));

        return Seq("LOOP", AltPrio(at, withControl));
    }
}
