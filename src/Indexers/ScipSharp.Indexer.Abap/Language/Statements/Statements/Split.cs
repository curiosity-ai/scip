// Ported from abaplint packages/core/src/abap/2_statements/statements/split.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Split : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var mode = Seq("IN", Alt("CHARACTER", "BYTE"), "MODE");

        var into = AltPrio(Seq("TABLE", Expr<Target>(), Opt(mode)), Seq(Plus(Expr<Target>()), Opt(mode)));

        var ret = Seq("SPLIT", Expr<Source>(), "AT", Expr<Source>(), "INTO", into);

        return ret;
    }
}
