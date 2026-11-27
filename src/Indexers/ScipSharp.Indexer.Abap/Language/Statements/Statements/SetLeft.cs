// Ported from abaplint packages/core/src/abap/2_statements/statements/set_left.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SetLeft : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var column = Seq("COLUMN", Expr<Source>());
        return VerNotLang(LanguageVersion.Cloud, Seq("SET LEFT SCROLL-BOUNDARY", Opt(column)));
    }
}
