// Ported from abaplint packages/core/src/abap/2_statements/statements/exec_sql.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ExecSQL : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var performing = Seq("PERFORMING", Expr<SimpleName>());

        var ret = Seq("EXEC SQL", Opt(performing));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
