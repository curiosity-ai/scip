// Ported from abaplint packages/core/src/abap/2_statements/expressions/with_name.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class WithName : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        // ( has no leading space, ) has leading space
        var colList = Seq(Tok<Tokens.ParenLeftW>(), Expr<SQLFieldName>(), Star(Seq(",", Expr<SQLFieldName>())), Tok<Tokens.WParenRightW>());
        return Seq(Tok<Tokens.WPlus>(), Regex(@"^\w+$"), OptPrio(colList));
    }
}
