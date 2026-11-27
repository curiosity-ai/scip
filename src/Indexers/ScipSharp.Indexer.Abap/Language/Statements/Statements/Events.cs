// Ported from abaplint packages/core/src/abap/2_statements/statements/events.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Events : IStatement
{

    public IStatementRunnable GetMatcher()
    {

        var exporting = Seq("EXPORTING", Plus(Expr<MethodParamOptional>()));

        return Seq(AltPrio("CLASS-EVENTS", "EVENTS"), Expr<EventName>(), OptPrio(exporting));
    }
}
