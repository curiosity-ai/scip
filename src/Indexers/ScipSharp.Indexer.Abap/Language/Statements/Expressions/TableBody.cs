// Ported from abaplint packages/core/src/abap/2_statements/expressions/table_body.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class TableBody : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var ret = Seq(Tok<Tokens.BracketLeft>(), AltPrio(Tok<Tokens.BracketRightW>(), Tok<Tokens.BracketRight>()));
        return ret;
    }
}
