// Ported from abaplint packages/core/src/abap/2_statements/statements/demand.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Demand : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var field = Seq(Expr<Expressions.Field>(), "=", Expr<Target>());

        var messages = Seq("MESSAGES INTO", Expr<Target>());

        var ret = Seq("DEMAND",
                        Plus(field),
                        "FROM CONTEXT",
                        Expr<Expressions.Field>(),
                        Opt(messages));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
