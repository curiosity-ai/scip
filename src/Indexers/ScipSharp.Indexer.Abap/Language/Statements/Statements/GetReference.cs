// Ported from abaplint packages/core/src/abap/2_statements/statements/get_reference.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class GetReference : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var ret = Seq("GET REFERENCE OF",
                        Expr<Source>(),
                        "INTO",
                        Expr<Target>());

        return VerNotLang(LanguageVersion.KeyUser, ret);
    }
}
