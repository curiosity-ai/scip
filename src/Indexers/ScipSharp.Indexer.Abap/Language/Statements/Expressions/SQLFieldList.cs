// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_field_list.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLFieldList : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var @as = Seq("AS", Expr<SQLAsName>());
        var commaParenField = Seq(Tok<Tokens.ParenLeftW>(), Expr<SQLFieldName>(), AltPrio(Tok<Tokens.WParenRightW>(), Tok<Tokens.WParenRight>()), OptPrio(@as));
        var nev = Ver(Release.v740sp05, StarPrio(Seq(",", AltPrio(Expr<SQLField>(), commaParenField))), also: AlsoIn.OpenABAP);
        var old = StarPrio(Seq(StopBefore("UP", "TO"), Expr<SQLField>()));

        return AltPrio("*",
                       Expr<Dynamic>(),
                       Seq(Expr<SQLField>(), Alt(nev, old)));
    }
}
