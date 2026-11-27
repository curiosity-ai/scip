// Ported from abaplint packages/core/src/abap/2_statements/statements/concatenate.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Concatenate : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var mode = Seq("IN", AltPrio("BYTE", "CHARACTER"), "MODE");
        var blanks = Str("RESPECTING BLANKS");
        var sep = Seq("SEPARATED BY", Expr<SimpleSource3>());

        var options = Per(mode, blanks, sep);

        var sourc = Seq(Expr<SimpleSource3>(), Plus(Expr<SimpleSource3>()));
        var lines = Seq("LINES OF", Expr<Source>());

        return Seq("CONCATENATE",
                   AltPrio(lines, sourc),
                   "INTO",
                   Expr<Target>(),
                   OptPrio(options));
    }
}
