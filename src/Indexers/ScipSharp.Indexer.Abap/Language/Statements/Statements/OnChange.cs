// Ported from abaplint packages/core/src/abap/2_statements/statements/on_change.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class OnChange : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var or = Seq("OR", Expr<Target>());

        var ret = Seq("ON CHANGE OF", Expr<Target>(), Star(or));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
