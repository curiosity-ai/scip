// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_source_simple.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

// todo, delete this expression, its the same as SQLSource?
public class SQLSourceSimple : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var paren = Seq(Tok<Tokens.ParenLeftW>(), Expr<Source>(), Tok<Tokens.WParenRightW>());

        var at = Ver(Release.v740sp05, Seq(Tok<Tokens.WAt>(), Alt(Expr<SimpleSource3>(), paren)), also: AlsoIn.OpenABAP);

        return Alt(Expr<SimpleSource3>(), at);
    }
}
