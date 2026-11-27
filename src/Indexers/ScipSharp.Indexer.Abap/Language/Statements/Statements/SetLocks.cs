// Ported from abaplint packages/core/src/abap/2_statements/statements/set_locks.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SetLocks : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var s = Seq("SET LOCKS OF",
                      Expr<SimpleName>(),
                      "ENTITY",
                      Expr<SimpleName>(),
                      "FROM", Expr<Source>(),
                      "FAILED", Expr<Target>(),
                      "REPORTED", Expr<Target>());
        return VerNotLang(LanguageVersion.KeyUser, Ver(Release.v754, s));
    }
}
