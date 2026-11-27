// Ported from abaplint packages/core/src/abap/2_statements/statements/shift.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Shift : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var deleting = Seq("DELETING", AltPrio("LEADING", "TRAILING"), Expr<Source>());
        var up = Seq("UP TO", Expr<Source>());
        var mode = Seq("IN", AltPrio("CHARACTER", "BYTE"), "MODE");
        var dir = AltPrio("LEFT", "RIGHT");
        var by = Seq("BY", Expr<Source>(), OptPrio("PLACES"));

        var options = Per(deleting, up, mode, dir, by, "CIRCULAR");

        return Seq("SHIFT",
                   Expr<Target>(),
                   Opt(options));
    }
}
