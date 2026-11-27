// Ported from abaplint packages/core/src/abap/2_statements/statements/get.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Get : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var fields = Seq("FIELDS", Plus(Expr<Expressions.Field>()));

        var options = Per("LATE", fields);

        var ret = Seq("GET",
                        Expr<Target>(),
                        Opt(options));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
