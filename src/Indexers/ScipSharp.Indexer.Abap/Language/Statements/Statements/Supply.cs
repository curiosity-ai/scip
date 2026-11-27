// Ported from abaplint packages/core/src/abap/2_statements/statements/supply.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Supply : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var field = Seq(Expr<Expressions.Field>(), "=", Expr<Source>());

        var ret = Seq("SUPPLY",
                        Plus(field),
                        "TO CONTEXT",
                        Expr<Expressions.Field>());

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
