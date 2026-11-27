// Ported from abaplint packages/core/src/abap/2_statements/statements/close_cursor.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class CloseCursor : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var ret = Seq("CLOSE CURSOR", Expr<SQLSourceSimple>());
        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
