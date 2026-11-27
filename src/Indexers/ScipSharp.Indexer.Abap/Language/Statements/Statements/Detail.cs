// Ported from abaplint packages/core/src/abap/2_statements/statements/detail.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Detail : IStatement
{
    public IStatementRunnable GetMatcher()
    {
        return VerNotLang(LanguageVersion.Cloud, "DETAIL");
    }
}
