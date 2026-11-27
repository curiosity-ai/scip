// Ported from abaplint packages/core/src/abap/2_statements/expressions/message_class.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class MessageClass : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        // "&1" can be used for almost anything(field names, method names etc.) in macros
        return Seq(Regex(@"^>?[\w\/]+#?@?\/?!?&?>?\$?\??<?§?~?$"), StarPrio(Tok<Tokens.Plus>()), StarPrio(Tok<Tokens.PlusW>()), StarPrio(Seq(Tok<Tokens.Dash>(), OptPrio(Regex(@"^\w+$")))), OptPrio(Tok<Tokens.DashW>()));
    }
}
