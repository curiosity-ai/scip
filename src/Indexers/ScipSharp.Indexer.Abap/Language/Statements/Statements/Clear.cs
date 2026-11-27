// Ported from abaplint packages/core/src/abap/2_statements/statements/clear.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Clear : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var wit = Seq("WITH", Expr<Source>());

        var mode = AltPrio("IN CHARACTER MODE", "IN BYTE MODE");

        return Seq("CLEAR",
                   Expr<Target>(),
                   OptPrio(wit),
                   OptPrio(mode));
    }
}
