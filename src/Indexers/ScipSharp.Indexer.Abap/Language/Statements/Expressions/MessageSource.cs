// Ported from abaplint packages/core/src/abap/2_statements/expressions/message_source.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class MessageSource : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var msgid = Seq(Tok<Tokens.ParenLeft>(), Expr<MessageClass>(), Tok<Tokens.ParenRightW>());
        var simple = Seq(Expr<MessageTypeAndNumber>(), Opt(msgid));

        var mess1 = Seq("ID", Expr<SimpleSource3>(), "TYPE", Expr<SimpleSource3>(), "NUMBER", AltPrio(Expr<MessageNumber>(), Expr<SimpleSource3>()));

        return AltPrio(simple, mess1);
    }
}
