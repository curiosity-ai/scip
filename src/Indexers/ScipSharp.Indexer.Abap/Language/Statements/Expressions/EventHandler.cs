// Ported from abaplint packages/core/src/abap/2_statements/expressions/event_handler.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class EventHandler : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var @event = Seq("FOR EVENT",
                          Expr<EventName>(),
                          "OF",
                          Expr<ClassName>(),
                          OptPrio(Seq("IMPORTING", PlusPrio(Expr<MethodParamName>()))));

        return @event;
    }
}
