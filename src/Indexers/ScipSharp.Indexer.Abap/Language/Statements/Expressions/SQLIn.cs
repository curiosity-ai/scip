// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_in.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLIn : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var val = new SQLSource();
        var @short = new SQLSourceNoSpace();

        var listOld = Seq(Tok<Tokens.WParenLeft>(), Alt(Ver(Release.v740sp05, @short, also: AlsoIn.OpenABAP), val), StarPrio(Seq(",", val)), AltPrio(Tok<Tokens.ParenRight>(), Tok<Tokens.ParenRightW>(), Tok<Tokens.WParenRightW>()));
        var listNew = Seq(Tok<Tokens.WParenLeftW>(), val, StarPrio(Seq(",", AltPrio(@short, val))), AltPrio(Tok<Tokens.WParenRight>(), Tok<Tokens.WParenRightW>()));
        // version is a guess, https://github.com/abaplint/abaplint/issues/2530
        var listNeww = Ver(Release.v740sp02, listNew, also: AlsoIn.OpenABAP);

        var subSelect = Expr<SQLSetOpGroup>();
        // simple subquery for pre-v750 versions: IN ( SELECT ... ), https://github.com/abaplint/abaplint/issues/3999
        var simpleSubSelect = Seq("(", "SELECT", SelectCore.BuildSelectCore(default, false), ")");

        var inn = Seq("IN", AltPrio(subSelect, simpleSubSelect, listOld, listNeww, Expr<SQLSource>()));

        return inn;
    }
}
