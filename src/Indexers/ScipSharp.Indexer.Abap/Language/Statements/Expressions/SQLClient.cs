// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_client.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLClient : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var clientList = Ver(Release.v740sp05, Alt(Expr<Dynamic>(), Seq(Expr<SQLAliasField>(), StarPrio(Seq(",", Expr<SQLAliasField>())))));

        var client = VerNotLang(LanguageVersion.KeyUser, Alt(
          VerNotLang(LanguageVersion.Cloud, Seq("CLIENT SPECIFIED", OptPrio(clientList))),
          Seq("USING", Alt(Ver(Release.v740sp05, Seq("CLIENT", Expr<SQLSourceSimple>())),
                           Ver(Release.v754, Seq("CLIENTS IN", Alt(Expr<SQLSourceSimple>(), "T000"))),
                           Ver(Release.v754, "ALL CLIENTS")))));
        return client;
    }
}
