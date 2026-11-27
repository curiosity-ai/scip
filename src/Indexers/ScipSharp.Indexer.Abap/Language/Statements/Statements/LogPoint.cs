// Ported from abaplint packages/core/src/abap/2_statements/statements/log_point.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class LogPoint : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var subkey = Seq("SUBKEY", Expr<Source>());

        var fields = Seq("FIELDS", Plus(Expr<Source>()));

        var ret = Seq("LOG-POINT ID",
                        Expr<NamespaceSimpleName>(),
                        Opt(subkey),
                        Opt(fields));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
