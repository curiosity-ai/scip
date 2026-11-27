// Ported from abaplint packages/core/src/abap/2_statements/statements/test_seam.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class TestSeam : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        return VerNotLang(LanguageVersion.KeyUser, Ver(Release.v750, Seq("TEST-SEAM", Expr<TestSeamName>()), also: AlsoIn.OpenABAP));
    }
}
