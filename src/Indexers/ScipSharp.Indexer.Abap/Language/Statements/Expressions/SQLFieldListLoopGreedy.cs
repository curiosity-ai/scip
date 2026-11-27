// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_field_list_loop_greedy.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLFieldListLoopGreedy : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var comma = Opt(Ver(Release.v740sp05, ",", also: AlsoIn.OpenABAP));
        var @as = Seq("AS", Expr<SQLAsName>());
        var abap = Ver(Release.v740sp05, Seq(Tok<Tokens.WAt>(), Expr<SimpleFieldChain2>()), also: AlsoIn.OpenABAP);
        var entry = Seq(AltPrio(Expr<SQLField>(), abap, Expr<SQLPathForColumn>(), Expr<SQLFieldName>(), Expr<Constant>()), OptPrio(@as), comma);
        var fieldList = PlusPrio(entry);

        return AltPrio("*", Expr<Dynamic>(), fieldList);
    }
}
