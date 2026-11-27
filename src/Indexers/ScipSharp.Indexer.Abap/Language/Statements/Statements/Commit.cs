// Ported from abaplint packages/core/src/abap/2_statements/statements/commit.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Commit : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var work = Seq("WORK", Opt("AND WAIT"));

        return VerNotLang(LanguageVersion.KeyUser, Seq("COMMIT", Alt(work, Expr<DatabaseConnection>())));
    }
}
