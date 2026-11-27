// Ported from abaplint packages/core/src/abap/2_statements/statements/open_cursor.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class OpenCursor : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var privileged = Ver(Release.v752, Seq("WITH", Expr<SQLPrivilegedAccess>()));

        var ret = Seq("OPEN CURSOR",
                        OptPrio("WITH HOLD"),
                        Expr<SQLTarget>(),
                        "FOR",
                        Expr<Expressions.Select>(),
                        OptPrio(privileged),
                        OptPrio(Expr<SQLOptions>()));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
