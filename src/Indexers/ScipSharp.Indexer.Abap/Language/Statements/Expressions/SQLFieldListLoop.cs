// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_field_list_loop.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

// loop must include one field from the database table
public class SQLFieldListLoop : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var comma = Opt(Ver(Release.v740sp05, ",", also: AlsoIn.OpenABAP));
        var @as = Seq("AS", Expr<SQLAsName>());
        var someField = Seq(Expr<SQLField>(), comma);
        var abap = Ver(Release.v740sp05, Seq(Tok<Tokens.WAt>(), Expr<SimpleFieldChain2>()), also: AlsoIn.OpenABAP);
        var fieldList = Seq(Star(someField), Alt(Expr<SQLFieldName>(), abap, Expr<SQLPathForColumn>(), Expr<Constant>()), OptPrio(@as), comma, Star(someField));

        var fields = Alt("*", Expr<Dynamic>(), fieldList);
        return fields;
    }
}
