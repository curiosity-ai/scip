// Ported from abaplint packages/core/src/abap/2_statements/statements/get_permissions.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class GetPermissions : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var type = AltPrio("GLOBAL AUTHORIZATION", "INSTANCE");

        var from = Seq("FROM", Expr<Source>());

        var s = Seq("GET PERMISSIONS ONLY", type, "ENTITY",
                      Expr<SimpleName>(),
                      OptPrio(from),
                      "REQUEST", Expr<Source>(),
                      "RESULT", Expr<Target>(),
                      "FAILED", Expr<Target>(),
                      "REPORTED", Expr<Target>());
        return Ver(Release.v754, s, also: AlsoIn.OpenABAP);
    }
}
