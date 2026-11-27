// Ported from abaplint packages/core/src/abap/2_statements/expressions/message_source_source.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class MessageSourceSource : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        return Alt(Ver(Release.v740sp02, Expr<Source>(), also: AlsoIn.OpenABAP), Expr<SimpleSource3>());
    }
}
